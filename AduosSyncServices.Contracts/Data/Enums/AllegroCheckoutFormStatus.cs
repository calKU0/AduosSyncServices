using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.Data.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AllegroCheckoutFormStatus
    {
        BOUGHT,
        FILLED_IN,
        READY_FOR_PROCESSING,
        CANCELLED
    }
}