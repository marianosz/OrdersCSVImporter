namespace OrdersCSVImporter.API
{
    public class APIConfig
    {
        public APIServiceConfig RunnerService { get; set; }

        public APIServiceConfig OrderService { get; set; }

        public APIServiceConfig InventoryService { get; set; }

        public APIServiceConfig LocationService { get; set; }
    }

    public class APIServiceConfig
    {
        public string APIKey { get; set; }

        public string URL { get; set; }
    }
}
