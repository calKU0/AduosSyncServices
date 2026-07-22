using AduosSyncServices.Contracts.Data.Enums;

namespace AduosSyncServices.Contracts.Extensions
{
    public static class GaskaDeliveryCourierExtensions
    {
        public static bool RequiresCodAmount(this GaskaDeliveryCourier courier) =>
            courier == GaskaDeliveryCourier.FedexDropshippingPobranie;

        public static bool IsAvailableForHeadquarters(this GaskaDeliveryCourier courier) =>
            courier != GaskaDeliveryCourier.FedexDropshippingPobranie;
    }
}
