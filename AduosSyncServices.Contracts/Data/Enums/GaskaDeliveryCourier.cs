using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.Data.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GaskaDeliveryCourier
    {
        [Description("DPD")]
        Dpd,
        [Description("GLS")]
        Gls,
        [Description("FedEx")]
        Fedex,
        [Description("FedEx Dropshipping pobranie")]
        FedexDropshippingPobranie,
        [Description("Hellmann")]
        Hellmann,
        [Description("Schenker")]
        Schenker,
        [Description("Odbiór własny")]
        PersonalCollection
    }
}
