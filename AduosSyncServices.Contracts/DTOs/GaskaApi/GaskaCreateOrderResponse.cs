using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.Allegro.GaskaApi
{
    public class GaskaCreateOrderResponse
    {
        [JsonPropertyName("newOrders")]
        public List<int> NewOrders { get; set; } = new();

        [JsonPropertyName("result")]
        public int Result { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}