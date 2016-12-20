using System.Collections.Generic;

namespace OrdersCSVImporter.API.LocationService.Model
{
    public class ProductsLocations
    {
        public IList<string> NotFound { get; set; }

        public IList<ProductLocation> Results { get; set; }
    }
}
