using System.Collections.Generic;

namespace OrdersCSVImporter.API.RunnerService.Model
{
    public class NewRunnerRequest
    {
        public string SalesPerson { get; set; }

        public IEnumerable<RunnerItem> Items { get; set; }

        public int DestinationId { get; set; }

        public string Warehouse { get; set; }

        public string @Type { get; set; }
    }
}