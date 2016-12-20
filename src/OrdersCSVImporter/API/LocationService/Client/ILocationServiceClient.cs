using System.Collections.Generic;
using System.Threading.Tasks;
using OrdersCSVImporter.API.LocationService.Model;

namespace OrdersCSVImporter.API.LocationService.Client
{
    public interface ILocationServiceClient
    {
        Task<APIRequestResult<ProductsLocations>> GetProductsLocation(IList<string> inventoryIds);
    }
}