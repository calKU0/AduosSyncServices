using AduosSyncServices.ServicesManager.Helpers;
using AduosSyncServices.ServicesManager.Models;
using Xunit;

namespace AduosSyncServices.ServicesManager.Tests
{
    // Sale price = net x (1 + margin/100), rounded up to a full zloty minus one grosz. Must stay in
    // lock-step with the products service's OfferFactory pricing.
    public class SalePriceCalculatorTests
    {
        private static List<MarginRange> Ranges() => new()
        {
            new MarginRange { Min = 0m, Max = 50m, Margin = 20m },
            new MarginRange { Min = 50.01m, Max = 100m, Margin = 10m },
            new MarginRange { Min = 100.01m, Max = 999999m, Margin = 5m }
        };

        [Fact]
        public void Calculate_UsesMarginOfMatchingRange_AndRoundsUpMinusGrosz()
        {
            // 40 net, 20% margin => 48.00 -> ceiling 48 - 0.01 = 47.99
            Assert.Equal(47.99m, SalePriceCalculator.Calculate(40m, Ranges()));
        }

        [Fact]
        public void Calculate_PicksRangeByNetPrice()
        {
            // 80 net falls in the 50.01-100 range (10%) => 88.00 -> 87.99
            Assert.Equal(87.99m, SalePriceCalculator.Calculate(80m, Ranges()));
        }

        [Fact]
        public void Calculate_NetAboveAllRanges_UsesLastRange()
        {
            var ranges = new List<MarginRange>
            {
                new() { Min = 0m, Max = 50m, Margin = 20m },
                new() { Min = 50.01m, Max = 100m, Margin = 10m }
            };
            // 500 matches no range => last range (10%) => 550 -> 549.99
            Assert.Equal(549.99m, SalePriceCalculator.Calculate(500m, ranges));
        }

        [Fact]
        public void Calculate_NoRanges_ReturnsNetUnchanged()
            => Assert.Equal(123.45m, SalePriceCalculator.Calculate(123.45m, new List<MarginRange>()));

        [Fact]
        public void Calculate_NullRanges_ReturnsNetUnchanged()
            => Assert.Equal(10m, SalePriceCalculator.Calculate(10m, null));

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Calculate_NonPositiveNet_ReturnsNetUnchanged(int net)
            => Assert.Equal((decimal)net, SalePriceCalculator.Calculate(net, Ranges()));
    }
}
