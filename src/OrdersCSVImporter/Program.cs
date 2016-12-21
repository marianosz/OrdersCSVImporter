using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrdersCSVImporter.API;
using OrdersCSVImporter.API.LocationService.Client;
using OrdersCSVImporter.API.RunnerService.Client;
using OrdersCSVImporter.API.RunnerService.Model;
using OrdersCSVImporter.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TinyCsvParser;
using TinyCsvParser.Mapping;
using System.ComponentModel;

namespace OrdersCSVImporter
{
    public class Program
    {
        static IServiceProvider serviceProvider;
        static IConfigurationRoot configuration;

        public static void Main(string[] args)
        {
            ILogger<Program> logger = null;

            try
            {
                Setup();

                logger = serviceProvider.GetService<ILoggerFactory>()
                    .CreateLogger<Program>();

                var argsResult = CommandLine.Parser.Default.ParseArguments<Options>(args);

                argsResult.WithParsed(x =>
                {
                    if (x.Help)
                    {
                        Console.WriteLine(CommandLine.Text.HelpText.AutoBuild(argsResult));
                        return;
                    }

                    logger.LogError($"'Days from' value {x.DaysFrom}");
                    logger.LogError($"'Runner Request quantity' value {x.RunnerRequestQuantity}");

                    var startDate = DateTime.Now.Subtract(new TimeSpan(x.DaysFrom, 0, 0, 0));

                    ImportFile(logger, startDate, x.RunnerRequestQuantity).Wait();
                });
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.LogError($"Exception; {ex.ToString()}");
                }
                else
                {
                    Console.WriteLine($"Startup Exception; {ex.ToString()}");
                }
            }
        }

        static async Task ImportFile(ILogger<Program> logger, DateTime startDate, int runnerRequestToCreateCount)
        {
            var csvData = await GetCSVData(logger, startDate);

            if (string.IsNullOrEmpty(csvData))
            {
                logger.LogError("Couldn´t retrieve csv data form magento. Aborting process!");
                return;
            }

            var allItems = ParseCSVData(logger, csvData);

            var pageCount = (allItems.Count() / runnerRequestToCreateCount);

            if ((allItems.Count() % runnerRequestToCreateCount) != 0)
            {
                pageCount++;
            }

            for (int i = 0; i < pageCount; i++)
            {
                logger.LogInformation($"Working with page {i + 1} of {runnerRequestToCreateCount} items");

                var pageItems = allItems
                    .Skip(i * runnerRequestToCreateCount)
                    .Take(runnerRequestToCreateCount).ToList();

                var itemsWithLocations = await GetItemLocations(logger, pageItems);

                if (itemsWithLocations == null)
                {
                    logger.LogError("Error getting locations. Aborting process!");
                    return;
                }

                var completed = await CreateRunnerRequests(logger, itemsWithLocations, runnerRequestToCreateCount);
            }

            logger.LogInformation("All done!");
        }

        static async Task<List<OrderInputData>> GetItemLocations(ILogger<Program> logger, List<OrderInputData> orderInputData)
        {
            var locationServiceClient = serviceProvider.GetService<ILocationServiceClient>();

            var orderInputSerializedIds = orderInputData.Select(x => x.SerializedId.Remove(0, 1)).ToList();

            logger.LogInformation("Getting the items locations...");

            var getProductsLocationResult = await locationServiceClient.GetProductsLocation(orderInputSerializedIds);

            if (getProductsLocationResult.HasError)
            {
                logger.LogError(getProductsLocationResult.ErrorMessage);
                return null;
            }

            logger.LogInformation("Locations obtained!");

            var locationData = getProductsLocationResult.Data;

            Parallel.ForEach(locationData.Results, r =>
            {
                var e = orderInputData.Where(x => x.SerializedId.Contains(r.product)).FirstOrDefault();

                if (e != null)
                {
                    e.LocationCode = r.locationCode;
                }
            });

            return orderInputData;
        }

        static async Task<bool> CreateRunnerRequests(ILogger<Program> logger, List<OrderInputData> orderInputData, int runnerRequestToCreateCount)
        {
            var itemsToRequest = orderInputData.Where(InValidLocation);

            var runnerServiceClient = serviceProvider.GetService<IRunnerServiceClient>();

            logger.LogInformation($"Saving {itemsToRequest.Count()} Runner Request...");

            var count = 0;

            foreach (var r in itemsToRequest.Where(x => !string.IsNullOrEmpty(x.LocationCode)))
            {
                if (count == runnerRequestToCreateCount)
                    return true;

                var runnerRequest = new NewRunnerRequest()
                {
                    SalesPerson = "Magento",
                    Warehouse = r.SerializedId.First() == 'N' ? "NY" : "LA",
                    @Type = "WEB",
                    DestinationId = r.SerializedId.First() == 'N' ? int.Parse(configuration["NYShippingLocation"]) : int.Parse(configuration["LAShippingLocation"]),
                    Items = new List<RunnerItem>()
                     {
                          new RunnerItem()
                          {
                              SerializedId = r.SerializedId.Remove(0, 1),
                          }
                     }
                };

                var postNewRunnerRequestResult = await runnerServiceClient.PostNewRunnerRequest(runnerRequest);

                if (postNewRunnerRequestResult.HasError)
                {
                    logger.LogError($"Error creating request for Serialized Id {r.SerializedId}: {postNewRunnerRequestResult.ErrorMessage}");
                }
                else
                {
                    logger.LogInformation($"Request created for Serialized Id {r.SerializedId}");
                    count++;
                }
            }

            return false;
        }

        static List<OrderInputData> ParseCSVData(ILogger<Program> logger, string csv)
        {
            var csvParserOptions = new CsvParserOptions(true, new[] { ',' });
            var csvMapper = new CsvOrderInputDataMapping();
            var csvReadOptions = new CsvReaderOptions(new[] { "\n" });
            var csvParser = new CsvParser<OrderInputData>(csvParserOptions, csvMapper);

            csv = csv.Replace("\"", "");

            return csvParser
                .ReadFromString(csvReadOptions, csv)
                .Select(x => x.Result)
                .Where(IsShoe)
                .OrderBy(OrderByShippingMethod)
                .ThenBy(x => x.CreatedAt)
                .ToList();
        }

        static int OrderByShippingMethod(OrderInputData value)
        {
            string MatrixrateExpressSaver = "matrixrate_express_saver";
            string MatrixratePriorityOvernight = "matrixrate_priority_overnight";

            if (value.ShippingMethod == MatrixratePriorityOvernight)
                return 0;

            if (value.ShippingMethod == MatrixrateExpressSaver)
                return 1;

            return 2;
        }

        static async Task<string> GetCSVData(ILogger<Program> logger, DateTime startDate)
        {
            var requestCSVFileCreationResult = await RequestCSVFileCreation(logger, startDate);

            if (!requestCSVFileCreationResult)
            {
                return null;
            }

            return await DownloadCSVFile(logger);
        }

        static bool IsShoe(OrderInputData data)
        {
            var sid = GetSIDFromSerializedId(data.SerializedId);

            if (sid.StartsWith("7"))
                return false;

            return true;
        }

        static bool InValidLocation(OrderInputData data)
        {
            if (string.IsNullOrEmpty(data.LocationCode))
                return false;

            if (data.LocationCode.Contains("STAGE"))
                return false;

            if (data.LocationCode.Contains("STAGING"))
                return false;

            if (data.LocationCode.Contains("SALESFLOOR"))
                return false;

            if (data.LocationCode.Contains("CONSIGNMENT"))
                return false;

            if (data.LocationCode.Contains("MISSING"))
                return false;

            if (data.LocationCode.Contains("SOLD"))
                return false;

            return true;
        }

        static string GetSIDFromSerializedId(string serializedId)
        {
            return serializedId.Substring(1, 6);
        }

        static async Task<bool> RequestCSVFileCreation(ILogger<Program> logger, DateTime from)
        {
            logger.LogInformation("Requesting CSV File Creation...");

            long afterValue = (long)(from - new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;

            var requestMessage = new HttpRequestMessage(new HttpMethod("GET"), $"http://ecommerce-admin.flightclub.com/run.orders.php?after={afterValue}");

            using (var client = new HttpClient())
            {
                var httpResponseMessage = await client.SendAsync(requestMessage);

                var result = await httpResponseMessage.Content.ReadAsStringAsync();

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    logger.LogInformation("CSV File created!");

                    return true;
                }

                logger.LogError($"Error requesting CSV file creation: {result}");

                return false;
            }
        }

        static async Task<string> DownloadCSVFile(ILogger<Program> logger)
        {

            logger.LogInformation("Downloading CSV File...");

            var requestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://flightclub.com/media/orders.csv");

            using (var client = new HttpClient())
            {
                var httpResponseMessage = await client.SendAsync(requestMessage);

                var fileResponse = await httpResponseMessage.Content.ReadAsStringAsync();

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    logger.LogInformation("CSV File downloaded!");

                    return fileResponse;
                }
                else
                {
                    logger.LogError($"Error downloading CSV file: {fileResponse}");
                    return null;
                }
            }
        }

        static void Setup()
        {
            var builder = new ConfigurationBuilder()
                          .SetBasePath(Path.Combine(AppContext.BaseDirectory, "../../../"))
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddEnvironmentVariables();

            configuration = builder.Build();

            serviceProvider = new ServiceCollection()
                .AddOptions()
                .Configure<APIConfig>(configuration.GetSection("API"))
                .AddLogging()
                .AddSingleton<ILocationServiceClient, LocationServiceClient>()
                .AddSingleton<IRunnerServiceClient, RunnerServiceClient>()
                .BuildServiceProvider();

            serviceProvider
                .GetService<ILoggerFactory>()
                .AddConsole(LogLevel.Debug);
        }

        class CsvOrderInputDataMapping : CsvMapping<OrderInputData>
        {
            public CsvOrderInputDataMapping()
                : base()
            {
                MapProperty(0, x => x.OrderId);
                MapProperty(1, x => x.InvoiceId);
                MapProperty(2, x => x.CreatedAt);
                MapProperty(3, x => x.ShippingMethod);
                MapProperty(4, x => x.SerializedId);
            }
        }

        class Options
        {
            [Option('d', "daysFrom", Required = false, Default = 30,
            HelpText = "Days from today to get the date to retrieve the CSV.")]
            public int DaysFrom { get; set; }

            [Option('r', "runnerRequestQuantity", Required = false, Default = 300,
            HelpText = "Quantity of runner request to create")]
            public int RunnerRequestQuantity { get; set; }

            [Option('h', "help", HelpText = "Prints this help", Required = false, Default = false)]
            public bool Help { get; set; }
        }
    }
}