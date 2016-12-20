using System.Collections.Generic;
using System.Threading.Tasks;
using OrdersCSVImporter.API.Client;
using OrdersCSVImporter.API.InventoryService.Model;

namespace OrdersCSVImporter.API.InventoryService.Client
{
    public interface IInventoryServiceClient
    {
        Task<APIRequestResult<Product>> GetProduct(string serializedId);
    }
}