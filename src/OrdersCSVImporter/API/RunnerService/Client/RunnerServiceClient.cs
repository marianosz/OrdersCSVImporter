using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrdersCSVImporter.API.Client;
using OrdersCSVImporter.API.RunnerService.Model;
using System;

namespace OrdersCSVImporter.API.RunnerService.Client
{
    public class RunnerServiceClient : BaseJsonServiceClient, IRunnerServiceClient
    {
        public RunnerServiceClient(IOptions<APIConfig> options, ILogger<RunnerServiceClient> logger)
            : base(options.Value.RunnerService, logger)
        {
        }

        public override async Task<string> GetErrorMessage(string jsonResponse)
            => (await DeserializeObject<ErrorMessage>(jsonResponse)).Message;

        public Task<APIRequestResult> PostNewRunnerRequest(NewRunnerRequest runnerRequest)
        {
            return Post<NewRunnerRequest>($"cheffing/request", runnerRequest);
        }

        public Task<APIRequestResult> RefreshLocations()
        {
            return Post<string>($"cheffing/request/type/web/refresh", string.Empty);
        }
        
        public Task<APIRequestResult<List<FullCheffingItem>>> GetUnassignedWebRunnerRequests(string warehouse)
        {
            return Get<List<FullCheffingItem>>($"cheffing/item/warehouse/{warehouse}/type/WEB/unassigned");
        }
    }
}