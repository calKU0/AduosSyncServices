using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.Data.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GaskaDeliveryCourier
    {
        Dpd,
        Gls,
        Fedex,
        FedexDropshippingPobranie,
        Hellmann,
        Schenker,
        PersonalCollection
    }
}
