using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.Data.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AllegroOrderStatus
    {
        [Description("Nowe")]
        NEW,
        [Description("W realizacji")]
        PROCESSING,
        [Description("Gotowe do wysyłki")]
        READY_FOR_SHIPMENT,
        [Description("Gotowe do odbioru")]
        READY_FOR_PICKUP,
        [Description("Wysłane")]
        SENT,
        [Description("Odebrane")]
        PICKED_UP,
        [Description("Anulowane")]
        CANCELLED,
        [Description("Wstrzymane")]
        SUSPENDED,
        [Description("Zwrócone")]
        RETURNED
    }
}