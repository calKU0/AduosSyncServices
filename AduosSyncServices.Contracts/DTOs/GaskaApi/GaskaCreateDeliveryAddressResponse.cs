using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.Allegro.GaskaApi
{
    public class GaskaCreateDeliveryAddressResponse
    {
        [JsonPropertyName("addressID")]
        public int AddressId { get; set; }

        [JsonPropertyName("result")]
        public int Result { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}