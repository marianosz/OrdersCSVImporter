﻿using CommandLine;
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
using System.Threading.Tasks;
using TinyCsvParser;
using TinyCsvParser.Mapping;
using NLog.Extensions.Logging;
using NLog.Config;
using OrdersCSVImporter.API.InventoryService.Client;
using System.Threading;

namespace OrdersCSVImporter
{
    public class Program
    {
        static IServiceProvider serviceProvider;
        static IConfigurationRoot configuration;

        public static void Main(string[] args)
        {
            ILogger<Program> logger = null;
            Timer timer = null;

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

                    logger.LogInformation($"'Days from' value {x.DaysFrom}");
                    logger.LogInformation($"'Runner Request quantity' value {x.RunnerRequestQuantity}");
                    logger.LogInformation($"'Minutes to Refresh' value {x.TimeToRefresh}");

                    var startDate = DateTime.Now.Subtract(new TimeSpan(x.DaysFrom, 0, 0, 0));

                    var autoEvent = new AutoResetEvent(false);

                    timer = new Timer(
                        (o) => {
                            ImportFile(logger, startDate, x.RunnerRequestQuantity).Wait();
                        }, null, 1000, x.TimeToRefresh * 60 * 1000);

                    autoEvent.WaitOne();
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
            finally
            {
                if (timer != null)
                {
                    timer.Dispose();
                }

                Console.WriteLine("Press a key to continue...");
                Console.ReadKey();
            }
        }

        static async Task ImportFile(ILogger<Program> logger, DateTime startDate, int runnerRequestToCreateCount)
        {
            try
            {
                if (await RunnerQueueHasSpace(logger, "NY", runnerRequestToCreateCount))
                {
                    var csvData = await GetCSVData(logger, startDate);

                    if (string.IsNullOrEmpty(csvData))
                    {
                        logger.LogError("Couldn´t retrieve csv data form magento. Aborting process!");
                        return;
                    }

                    var allItems = ParseCSVData(logger, csvData);

                    //var laItems = allItems.Where(x => x.SerializedId.StartsWith("L")).ToList();
                    var nyItems = allItems.Where(x => x.SerializedId.StartsWith("N")).ToList();

                    await CreateRunnerRequests(logger, nyItems, "NY", runnerRequestToCreateCount);
                    //await CreateRunnerRequests(logger, nyItems, "LA", runnerRequestToCreateCount);

                    await RefreshLocations(logger);
                }

                logger.LogInformation("Task finished");
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

                return;
            }
        }

        static async Task RefreshLocations(ILogger<Program> logger)
        {

            logger.LogInformation($"Refreshing locations...");

            var runnerServiceClient = serviceProvider.GetService<IRunnerServiceClient>();

            var refreshLocationsResult = await runnerServiceClient.RefreshLocations();

            if (refreshLocationsResult.HasError)
            {
                logger.LogError($"Error refreshing locations: {refreshLocationsResult.ErrorMessage}");
                return;
            }

            logger.LogInformation($"Locations Refreshed!");

        }

        //static async Task<List<OrderInputData>> GetShoeItems()
        //{
        //    foreach (var r in itemsToRequest)
        //    {
        //        if (itemsCount == runnerRequestToCreateCount)
        //        {
        //            return new
        //            {
        //                Finished = true,
        //                ItemsCount = itemsCount
        //            };
        //        }

        //        var runnerRequest = new NewRunnerRequest()
        //        {
        //            SalesPerson = "Magento",
        //            Warehouse = r.SerializedId.First() == 'N' ? "NY" : "LA",
        //            @Type = "WEB",
        //            DestinationId = r.SerializedId.First() == 'N' ? int.Parse(configuration["NYShippingLocation"]) : int.Parse(configuration["LAShippingLocation"]),
        //            Items = new List<RunnerItem>()
        //             {
        //                  new RunnerItem()
        //                  {
        //                      SerializedId = r.SerializedId.Remove(0, 1),
        //                  }
        //             }
        //        };

        //        var postNewRunnerRequestResult = await runnerServiceClient.PostNewRunnerRequest(runnerRequest);

        //        if (postNewRunnerRequestResult.HasError)
        //        {
        //            logger.LogError($"Error creating request for Serialized Id {r.SerializedId}: {postNewRunnerRequestResult.ErrorMessage}");
        //        }
        //        else
        //        {
        //            logger.LogInformation($"Request created for Serialized Id {r.SerializedId}");
        //            itemsCount++;
        //        }
        //    }

        //}

        static async Task<bool> RunnerQueueHasSpace(ILogger<Program> logger, string warehouse, int maxRunnerRequestToCreateCount)
        {
            var runnerServiceClient = serviceProvider.GetService<IRunnerServiceClient>();

            var getUnassignedWebRunnerRequests = await runnerServiceClient.GetUnassignedWebRunnerRequests(warehouse);

            if (getUnassignedWebRunnerRequests.HasError)
            {
                logger.LogError($"Couldn´t retrieve unasigned items. {getUnassignedWebRunnerRequests.ErrorMessage}. Aborting process!");
                return false;
            }

            var unassignedCount = getUnassignedWebRunnerRequests.Data.Count;

            if (unassignedCount >= maxRunnerRequestToCreateCount)
            {
                logger.LogInformation($"Queue is Full, unassigned request count: {unassignedCount}. Aborting process!");
                return false;
            }

            logger.LogInformation($"Current queue size: {unassignedCount}.");

            return true;
        }

        static async Task CreateRunnerRequests(ILogger<Program> logger, List<OrderInputData> orderInputData, string warehouse, int maxRunnerRequestToCreateCount)
        {
            var runnerServiceClient = serviceProvider.GetService<IRunnerServiceClient>();

            logger.LogInformation($"Creating {maxRunnerRequestToCreateCount} Running request...");

            var getUnassignedWebRunnerRequests = await runnerServiceClient.GetUnassignedWebRunnerRequests(warehouse);

            if (getUnassignedWebRunnerRequests.HasError)
            {
                logger.LogError($"Couldn´t retrieve unasigned items. {getUnassignedWebRunnerRequests.ErrorMessage}. Aborting process!");
                return;
            }

            var unassignedCount = getUnassignedWebRunnerRequests.Data.Count;

            if (unassignedCount >= maxRunnerRequestToCreateCount)
            {
                logger.LogInformation ($"Unassigned request count: {unassignedCount}. Aborting process!");
                return;
            }

            var runnerRequestToCreateCount = maxRunnerRequestToCreateCount - unassignedCount;

            var pageSize = 600;

            var pageCount = (orderInputData.Count / pageSize);

            if ((orderInputData.Count % pageSize) != 0)
            {
                pageCount++;
            }

            var itemsCount = 0;

            for (int i = 0; i < pageCount; i++)
            {
                logger.LogInformation($"Working with page {i + 1} of {pageCount} for a total of {runnerRequestToCreateCount} items");

                var pageItems = orderInputData
                    .Skip(i * pageSize)
                    .Take(pageSize).ToList();

                var itemsWithLocations = await GetItemLocations(logger, pageItems);

                if (itemsWithLocations == null)
                {
                    logger.LogError("Error getting locations. Aborting process!");
                    return;
                }

                var result = await CreateRunnerRequests(logger, itemsWithLocations, runnerRequestToCreateCount, itemsCount);

                if (result.Finished)
                {
                    break;
                }

                itemsCount = result.ItemsCount;
            }

            logger.LogInformation($"Created {itemsCount} Runner requests!");
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

        static async Task<dynamic> CreateRunnerRequests(ILogger<Program> logger, List<OrderInputData> orderInputData, int runnerRequestToCreateCount, int itemsCount)
        {
            var itemsToRequest = orderInputData.Where(InValidLocation);

            var runnerServiceClient = serviceProvider.GetService<IRunnerServiceClient>();

            logger.LogInformation($"Saving {itemsToRequest.Count()} Runner Request...");
            
            foreach (var r in itemsToRequest)
            {
                if (itemsCount == runnerRequestToCreateCount)
                {
                    return new
                    {
                        Finished = true,
                        ItemsCount = itemsCount
                    };
                }

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
                    if (postNewRunnerRequestResult.StatusCode == System.Net.HttpStatusCode.NoContent)
                    {
                        logger.LogInformation($"Request already exists for Serialized Id {r.SerializedId}");
                    }
                    else
                    {
                        logger.LogInformation($"Request created for Serialized Id {r.SerializedId}");
                        itemsCount++;
                    }
                }
            }

            return new
            {
                Finished = false,
                ItemsCount = itemsCount
            };

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
                //.Where(IsShoe)
                .OrderBy(OrderByShippingMethod)
                .ThenBy(OrderByShoeNonShoe)
                .ThenBy(x => x.CreatedAt)
                .ToList();
        }

        static int OrderByShippingMethod(OrderInputData value)
        {
            string Matrixrate2day = "matrixrate_2day ";
            string MatrixratePriorityOvernight = "matrixrate_priority_overnight";

            if (value.ShippingMethod == MatrixratePriorityOvernight)
                return 0;

            if (value.ShippingMethod == Matrixrate2day)
                return 1;

            return 2;
        }

        static int OrderByShoeNonShoe(OrderInputData value)
        {
            if (IsShoe(value))
                return 0;

            return 1;
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

            //if (data.LocationCode.Contains("SALESFLOOR"))
            //    return false;

            //if (data.LocationCode.Contains("CONSIGNMENT"))
            //    return false;

            if (data.LocationCode.Contains("MISSING"))
                return false;

            if (data.LocationCode.Contains("SOLD"))
                return false;

            if (data.LocationCode.Contains("CSTSRV"))
                return false;
            
            return true;
        }

        static string GetSIDFromSerializedId(string serializedId)
        {
            return serializedId.Substring(2, 6);
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
            //Random number to invalidate cache
            var random = new Random();
            int randomNumber = random.Next(0, 1000);

            logger.LogInformation("Downloading CSV File...");

            var requestMessage = new HttpRequestMessage(new HttpMethod("GET"), $"https://flightclub.com/media/orders.csv?nocache={randomNumber}");

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
                          .SetBasePath(AppContext.BaseDirectory)
                          .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddEnvironmentVariables();

            configuration = builder.Build();

            serviceProvider = new ServiceCollection()
                .AddOptions()
                .Configure<APIConfig>(configuration.GetSection("API"))
                .AddLogging()
                .AddSingleton<ILocationServiceClient, LocationServiceClient>()
                .AddSingleton<IRunnerServiceClient, RunnerServiceClient>()
                .AddSingleton<IInventoryServiceClient, InventoryServiceClient>()
                .BuildServiceProvider();

            serviceProvider
                .GetService<ILoggerFactory>()
                .AddConsole(LogLevel.Information)
                .AddConsole(LogLevel.Error)
                .AddNLog();

            NLog.LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine(Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationBasePath, "nlog.config"), true);
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
                MapProperty(5, x => x.Warehouse);
            }
        }

        class Options
        {
            [Option('d', "daysFrom", Required = false, Default = 30,
            HelpText = "Days from today to get the date to retrieve the CSV.")]
            public int DaysFrom { get; set; }

            [Option('r', "runnerRequestQuantity", Required = false, Default = 400,
            HelpText = "Max quantity of runner request to create")]
            public int RunnerRequestQuantity { get; set; }

            [Option('t', "timeToRefresh", Required = false, Default = 15,
            HelpText = "Time to run the refresh in minutes")]
            public int TimeToRefresh { get; set; }

            [Option('h', "help", HelpText = "Prints this help", Required = false, Default = false)]
            public bool Help { get; set; }
        }
    }
}