using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrdersCSVImporter.API.Client;
using OrdersCSVImporter.API.LocationService.Model;

namespace OrdersCSVImporter.API.LocationService.Client
{
    public class LocationServiceClient : BaseJsonServiceClient, ILocationServiceClient
    {
        public LocationServiceClient(IOptions<APIConfig> options, ILogger<LocationServiceClient> logger)
            : base(options.Value.LocationService, logger)
        {
        }

        public override async Task<string> GetErrorMessage(string jsonResponse)
            => (await DeserializeObject<Error>(jsonResponse)).Message;
        
        public Task<APIRequestResult<ProductsLocations>> GetProductsLocation(IList<string> inventoryIds)
        {
            return Post<IList<string>, ProductsLocations>("lookup/products", inventoryIds);

        }
    }
}