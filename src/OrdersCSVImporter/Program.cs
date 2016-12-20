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

namespace OrdersCSVImporter
{
    public class Program
    {
        static IServiceProvider serviceProvider;
        static IConfigurationRoot configuration;

        public static void Main(string[] args)
        {
            Setup();

            var logger = serviceProvider.GetService<ILoggerFactory>()
                .CreateLogger<Program>();

            logger.LogInformation("Starting tool");

            var daysFrom = 30;

            try
            {
                if (args.Length == 1)
                {
                    daysFrom = int.Parse(args[0]);
                }

                var startDate = DateTime.Now.Subtract(new TimeSpan(daysFrom, 0, 0, 0));

                ImportFile(logger, startDate).Wait();
            }
            catch (Exception ex)
            {
                logger.LogError($"Exception; {ex.ToString()}");
            }
        }

        static async Task ImportFile(ILogger<Program> logger, DateTime startDate)
        {
            var csvData = await GetCSVData(logger, startDate);

            if (string.IsNullOrEmpty(csvData))
            {
                logger.LogError("Couldn´t retrieve csv data form magento. Aborting process!");
                return;
            }

            var allItems = ParseCSVData(logger, csvData);

            var itemsSublist = allItems.Take(300).ToList();

            await GetItemLocations(logger, itemsSublist);

            await CreateRunnerRequests(logger, itemsSublist);

            logger.LogInformation("All done!");
        }

        static async Task<List<OrderInputData>> GetItemLocations(ILogger<Program> logger, List<OrderInputData> orderInputData)
        {
            var locationServiceClient = serviceProvider.GetService<ILocationServiceClient>();

            var orderInputSerializedIds = orderInputData.Select(x => x.SerializedId.Remove(0,1)).ToList();

            logger.LogInformation("Getting the items locations");

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

        static async Task CreateRunnerRequests(ILogger<Program> logger, List<OrderInputData> orderInputData)
        {
            var runnerServiceClient = serviceProvider.GetService<IRunnerServiceClient>();

            logger.LogInformation($"Saving {orderInputData.Count} Runner Request");

            foreach (var r in orderInputData.Where(x => !string.IsNullOrEmpty(x.LocationCode)))
            {
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
                }
            }
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
            logger.LogInformation("Request CSV File Creation");

            var requestCSVFileCreationResult = await RequestCSVFileCreation(logger, startDate);

            if (!requestCSVFileCreationResult)
            {
                return null;
            }

            logger.LogInformation("Download CSV File");

            return await DownloadCSVFile(logger);
        }

        static async Task<bool> RequestCSVFileCreation(ILogger<Program> logger, DateTime from)
        {
            long afterValue = (long) (from - new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;

            var requestMessage = new HttpRequestMessage(new HttpMethod("GET"), $"http://ecommerce-admin.flightclub.com/run.orders.php?after={afterValue}");

            using (var client = new HttpClient())
            {
                var httpResponseMessage = await client.SendAsync(requestMessage);

                var result = await httpResponseMessage.Content.ReadAsStringAsync();

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    return true;
                }

                logger.LogError($"Error requesting CSV file creation: {result}");

                return false;
            }
        }

        static async Task<string> DownloadCSVFile(ILogger<Program> logger)
        {
            var requestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://flightclub.com/media/orders.csv");

            using (var client = new HttpClient())
            {
                var httpResponseMessage = await client.SendAsync(requestMessage);

                var fileResponse = await httpResponseMessage.Content.ReadAsStringAsync();

                if (httpResponseMessage.IsSuccessStatusCode)
                {
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
    }
}