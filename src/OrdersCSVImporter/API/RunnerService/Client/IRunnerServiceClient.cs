using System.Collections.Generic;
using System.Threading.Tasks;
using OrdersCSVImporter.API.RunnerService.Model;

namespace OrdersCSVImporter.API.RunnerService.Client
{
    public interface IRunnerServiceClient
    {
        Task<APIRequestResult<RunnerRequest>> PostNewRunnerRequest(NewRunnerRequest runnerRequest);

        Task<APIRequestResult<List<FullCheffingItem>>> GetUnassignedWebRunnerRequests(string warehouse);
    }
}