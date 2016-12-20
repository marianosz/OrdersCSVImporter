using System.Collections.Generic;

namespace OrdersCSVImporter.API.InventoryService.Model
{
    public class Product
    {
        public string serialized_id { get; set; }
        public string notes { get; set; }
        public string sid { get; set; }
        public string style { get; set; }
        public string type { get; set; }
        public string brand { get; set; }
        public string model { get; set; }
        public int? year { get; set; }
        public string colorway { get; set; }
        public string size { get; set; }
        public bool sold { get; set; }
        public bool hidden { get; set; }
        public List<string> conditions { get; set; }
        public string warehouse { get; set; }
        public int price_cents { private get; set; }
        public decimal price
        {
            get
            {
                return price_cents / 100;
            }
        }
    }
}