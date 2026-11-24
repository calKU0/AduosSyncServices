using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    /// <summary>Przesyłki zgłoszone do zamówienia w Allegro (GET /order/checkout-forms/{id}/shipments).</summary>
    public class AllegroShipmentsResponse
    {
        [JsonPropertyName("shipments")]
        public List<Shipment> Shipments { get; set; } = new();

        public class Shipment
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("waybill")]
            public string? Waybill { get; set; }

            [JsonPropertyName("carrierId")]
            public string? CarrierId { get; set; }
        }
    }
}
