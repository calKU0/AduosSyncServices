using AduosSyncServices.Contracts.Data.Enums;
using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class AllegroSetOrderStatusRequest
    {
        [JsonPropertyName("status")]
        public AllegroOrderStatus Status { get; set; }
    }
}