using AduosSyncServices.Contracts.Data.Enums;

namespace AduosSyncServices.Contracts.Extensions
{
    public static class GaskaDeliveryCourierExtensions
    {
        public static string ToGaskaDeliveryMethod(this GaskaDeliveryCourier courier) => courier switch
        {
            GaskaDeliveryCourier.Dpd => "DPD",
            GaskaDeliveryCourier.Gls => "GLD",
            GaskaDeliveryCourier.Fedex => "FedEx",
            GaskaDeliveryCourier.FedexDropshippingPobranie => "FedEx Dropshipping pobranie",
            _ => throw new ArgumentOutOfRangeException(nameof(courier), courier, null)
        };

        public static string ToPolishDisplayName(this GaskaDeliveryCourier courier) => courier switch
        {
            GaskaDeliveryCourier.Dpd => "DPD",
            GaskaDeliveryCourier.Gls => "GLS",
            GaskaDeliveryCourier.Fedex => "FedEx",
            GaskaDeliveryCourier.FedexDropshippingPobranie => "FedEx Dropshipping (pobranie)",
            _ => throw new ArgumentOutOfRangeException(nameof(courier), courier, null)
        };

        public static bool RequiresCodAmount(this GaskaDeliveryCourier courier) =>
            courier == GaskaDeliveryCourier.FedexDropshippingPobranie;

        public static bool IsAvailableForHeadquarters(this GaskaDeliveryCourier courier) =>
            courier != GaskaDeliveryCourier.FedexDropshippingPobranie;
    }
}
