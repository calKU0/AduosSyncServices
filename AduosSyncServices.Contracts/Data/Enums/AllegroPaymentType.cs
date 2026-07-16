using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.Data.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AllegroPaymentType
    {
        [Description("Za pobraniem")]
        CASH_ON_DELIVERY,
        [Description("Przelew")]
        WIRE_TRANSFER,
        [Description("Płatność online")]
        ONLINE,
        [Description("Płatność podzielona")]
        SPLIT_PAYMENT,
        [Description("Odroczony termin płatności")]
        EXTENDED_TERM
    }
}