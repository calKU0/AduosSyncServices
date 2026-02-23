using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class AllegroAddTrackingNumberRequest
    {
        [JsonPropertyName("carrierId")]
        public string CarrierId { get; set; } = string.Empty;

        [JsonPropertyName("waybill")]
        public string Waybill { get; set; } = string.Empty;

        [JsonPropertyName("carrierName")]
        public string? CarrierName { get; set; }

        [JsonPropertyName("lineItems")]
        public List<LineItem>? LineItems { get; set; }

        public class LineItem
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
        }
    }
}