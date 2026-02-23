using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.Allegro.GaskaApi
{
    public class GaskaCreateDeliveryAddressRequest
    {
        [JsonPropertyName("name1")]
        public string Name1 { get; set; } = string.Empty;

        [JsonPropertyName("name2")]
        public string? Name2 { get; set; }

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("street")]
        public string Street { get; set; } = string.Empty;

        [JsonPropertyName("postCode")]
        public string PostalCode { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("oneUse")]
        public bool OneUse { get; set; } = true;
    }
}