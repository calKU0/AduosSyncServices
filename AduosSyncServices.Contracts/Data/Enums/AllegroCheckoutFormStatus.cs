using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.Data.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AllegroCheckoutFormStatus
    {
        [Description("Kupione")]
        BOUGHT,
        [Description("Wypełnione")]
        FILLED_IN,
        [Description("Gotowe do realizacji")]
        READY_FOR_PROCESSING,
        [Description("Anulowane")]
        CANCELLED
    }
}