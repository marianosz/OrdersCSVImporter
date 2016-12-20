using System;
using System.Collections.Generic;

namespace OrdersCSVImporter.API.RunnerService.Model
{ 
    public class RunnerRequest
    {
        public int Id { get; set; }

        public DateTime Created { get; set; }

        public DateTime? Updated { get; set; }

        public string UserId { get; set; }

        public string Customer { get; set; }

        public string Destination { get; set; }

        public virtual IList<RunnerItem> Items { get; set; }

        public object SalesPerson { get; set; }
    }
}