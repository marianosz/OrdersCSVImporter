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
            : base(options.Value.RunnerService, logger, true)
        {
        }

        public override async Task<string> GetErrorMessage(string jsonResponse)
            => (await DeserializeObject<ErrorMessage>(jsonResponse)).Message;

        public Task<APIRequestResult<RunnerRequest>> PostNewRunnerRequest(NewRunnerRequest runnerRequest)
        {
            return Post<NewRunnerRequest, RunnerRequest>($"cheffing/request", runnerRequest);
        }
    }
}