using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrdersCSVImporter.Model
{
    public class OrderInputData
    {
        public string OrderId { get; set; }

        public string InvoiceId { get; set; }

        public DateTime CreatedAt { get; set; }

        public string ShippingMethod { get; set; }

        public string SerializedId { get; set; }

        public string Warehouse { get; set; }

        public string LocationCode { get; set; }
    }
}
