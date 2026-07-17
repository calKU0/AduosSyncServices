using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.GaskaApi
{
    public class GaskaGetOrderRequest
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("lng")]
        public int Lng { get; set; }
    }
}