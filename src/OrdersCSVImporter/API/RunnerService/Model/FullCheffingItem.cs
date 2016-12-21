using Newtonsoft.Json;
using System;

namespace OrdersCSVImporter.API.RunnerService.Model
{
    public class FullCheffingItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("serializedId")]
        public string SerializedId { get; set; }

        [JsonProperty("sid")]
        public string SID { get; set; }

        [JsonProperty("locationCode")]
        public string LocationCode { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("salesPerson")]
        public string SalesPerson { get; set; }

        [JsonProperty("salesPersonId")]
        public string SalesPersonId { get; set; }

        [JsonProperty("customer")]
        public string Customer { get; set; }

        [JsonProperty("pickerUserId")]
        public string PickerUserId { get; set; }

        [JsonProperty("pickerUser")]
        public string PickerUser { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("destination")]
        public string Destination { get; set; }

        [JsonProperty("created")]
        public DateTime Created { get; set; }

        [JsonProperty("updated")]
        public DateTime? Updated { get; set; }

        [JsonProperty("brand")]
        public string Brand { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("colorway")]
        public string Colorway { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("warehouse")]
        public string Warehouse { get; set; }
    }
}