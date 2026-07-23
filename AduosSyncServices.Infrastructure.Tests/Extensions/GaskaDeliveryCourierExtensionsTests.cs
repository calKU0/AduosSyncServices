using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Extensions;
using Xunit;

namespace AduosSyncServices.Infrastructure.Tests.Extensions
{
    public class GaskaDeliveryCourierExtensionsTests
    {
        [Theory]
        [InlineData(GaskaDeliveryCourier.FedexDropshippingPobranie, true)]
        [InlineData(GaskaDeliveryCourier.Dpd, false)]
        [InlineData(GaskaDeliveryCourier.Gls, false)]
        [InlineData(GaskaDeliveryCourier.Fedex, false)]
        public void RequiresCodAmount_TrueOnlyForDropshippingPobranie(GaskaDeliveryCourier courier, bool expected)
            => Assert.Equal(expected, courier.RequiresCodAmount());

        [Theory]
        [InlineData(GaskaDeliveryCourier.FedexDropshippingPobranie, false)]
        [InlineData(GaskaDeliveryCourier.Dpd, true)]
        [InlineData(GaskaDeliveryCourier.PersonalCollection, true)]
        public void IsAvailableForHeadquarters_FalseOnlyForDropshippingPobranie(GaskaDeliveryCourier courier, bool expected)
            => Assert.Equal(expected, courier.IsAvailableForHeadquarters());

        [Theory]
        [InlineData(GaskaDeliveryCourier.Dpd, "DPD")]
        [InlineData(GaskaDeliveryCourier.Gls, "GLS")]
        [InlineData(GaskaDeliveryCourier.Fedex, "FedEx")]
        [InlineData(GaskaDeliveryCourier.FedexDropshippingPobranie, "FedEx Dropshipping pobranie")]
        public void GetDescription_ReturnsGaskaDeliveryMethodString(GaskaDeliveryCourier courier, string expected)
            => Assert.Equal(expected, courier.GetDescription());
    }
}
