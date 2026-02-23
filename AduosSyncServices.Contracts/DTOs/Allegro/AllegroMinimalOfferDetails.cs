using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class AllegroMinimalOfferDetails
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("delivery")]
        public DeliveryDto Delivery { get; set; }

        public class DeliveryDto
        {
            [JsonPropertyName("handlingTime")]
            public string HandlingTime { get; set; }

            [JsonPropertyName("shippingRates")]
            public ShippingRates ShippingRates { get; set; }

            [JsonPropertyName("additionalInfo")]
            public string AdditionalInfo { get; set; }

            [JsonPropertyName("shipmentDate")]
            public DateTime? ShipmentDate { get; set; }
        }

        public class ShippingRates
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
        }
    }
}
