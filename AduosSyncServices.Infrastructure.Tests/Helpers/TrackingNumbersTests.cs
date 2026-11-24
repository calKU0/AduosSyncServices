using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Helpers;
using AduosSyncServices.Infrastructure.Services;
using Xunit;

namespace AduosSyncServices.Infrastructure.Tests.Helpers
{
    public class TrackingNumbersTests
    {
        [Theory]
        [InlineData(null, new string[0])]
        [InlineData("  ", new string[0])]
        [InlineData("30648325292", new[] { "30648325292" })]
        [InlineData("30648325292, 30648325293,30648325296", new[] { "30648325292", "30648325293", "30648325296" })]
        [InlineData("A1; B2 ;; A1", new[] { "A1", "B2" })]
        public void Split_ReturnsSingleNumbers(string? field, string[] expected)
        {
            Assert.Equal(expected, TrackingNumbers.Split(field));
        }

        [Theory]
        [InlineData("Kurier DPD", "DPD")]
        [InlineData("fedex", "FEDEX")]
        [InlineData("GLS Poland", "GLS")]
        [InlineData("INPOST", "INPOST")]
        [InlineData(null, null)]
        public void NormalizeCarrierId_MapsKnownCouriers(string? courier, string? expected)
        {
            Assert.Equal(expected, TrackingNumbers.NormalizeCarrierId(courier));
        }

        [Fact]
        public void GroupShipments_SplitsCommaSeparatedNumbers_AndGroupsItemsPerNumber()
        {
            var items = new[]
            {
                new AllegroOrderItem { OrderItemId = "i1", ExternalCourier = "Kurier DPD", ExternalTrackingNumber = "111, 222" },
                new AllegroOrderItem { OrderItemId = "i2", ExternalCourier = "DPD", ExternalTrackingNumber = "222" },
                new AllegroOrderItem { OrderItemId = "i3", ExternalCourier = "DPD", ExternalTrackingNumber = null },
            };

            var shipments = AllegroOrderSyncService.GroupShipments(items);

            Assert.Equal(2, shipments.Count);
            var first = Assert.Single(shipments, s => s.Waybill == "111");
            Assert.Equal("DPD", first.CarrierId);
            Assert.Equal(new[] { "i1" }, first.LineItemIds);
            var second = Assert.Single(shipments, s => s.Waybill == "222");
            Assert.Equal(new[] { "i1", "i2" }, second.LineItemIds);
        }

        [Fact]
        public void ShipmentKey_IgnoresCase()
        {
            Assert.Equal(TrackingNumbers.ShipmentKey("dpd", "ab1"), TrackingNumbers.ShipmentKey("DPD", "AB1"));
        }
    }
}
