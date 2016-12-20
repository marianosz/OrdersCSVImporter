using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrdersCSVImporter.API.Client;
using OrdersCSVImporter.API.InventoryService.Model;

namespace OrdersCSVImporter.API.InventoryService.Client
{
    public class InventoryServiceClient : BaseJsonServiceClient, IInventoryServiceClient
    {
        public InventoryServiceClient(IOptions<APIConfig> options, ILogger<InventoryServiceClient> logger)
            : base(options.Value.InventoryService, logger)
        {
        }

        public override string ApiKeyHederName => "X-Beluga-Api-Key";

        public override async Task<string> GetErrorMessage(string jsonResponse)
            => (await DeserializeObject<Error>(jsonResponse)).error;

        public Task<APIRequestResult<Product>> GetProduct(string serializedId)
        {
            return Get<Product>($"v1/stock_items/serialized/{serializedId}");
        }
    }
}