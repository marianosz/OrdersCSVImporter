namespace OrdersCSVImporter.API.LocationService.Model
{
    public class ProductLocation
    {
        public string locationCode { get; set; }
        public string neighborhood { get; set; }
        public string street { get; set; }
        public string building { get; set; }
        public string floor { get; set; }
        public string warehouse { get; set; }
        public string product { get; set; }
        public string productStyle { get; set; }
        public bool available { get; set; }
    }
}
