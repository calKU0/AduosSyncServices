using AduosSyncServices.ServicesManager.Models;
using Xunit;

namespace AduosSyncServices.ServicesManager.Tests
{
    public class OrderItemRowViewModelTests
    {
        [Theory]
        [InlineData(0, "Standardowy")]
        [InlineData(1, "Gabarytowy")]
        [InlineData(2, "Niestandardowy")]
        public void FormatDeliveryType_KnownValues_ReturnDescription(int value, string expected)
            => Assert.Equal(expected, OrderItemRowViewModel.FormatDeliveryType(value));

        [Theory]
        [InlineData(null)]
        [InlineData(99)]
        [InlineData(-1)]
        public void FormatDeliveryType_UnknownOrNull_ReturnsDash(int? value)
            => Assert.Equal("-", OrderItemRowViewModel.FormatDeliveryType(value));

        [Fact]
        public void FormatPrice_ParsesInvariantString_AndFormatsPlPl()
        {
            // pl-PL uses a comma decimal separator and a non-breaking space thousands separator.
            var result = OrderItemRowViewModel.FormatPrice("1234.5");
            Assert.EndsWith("zł", result);
            Assert.Contains("34,50", result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void FormatPrice_BlankInput_ReturnsDash(string input)
            => Assert.Equal("-", OrderItemRowViewModel.FormatPrice(input));

        [Fact]
        public void FormatPrice_Unparseable_ReturnsRawValue()
            => Assert.Equal("brak", OrderItemRowViewModel.FormatPrice("brak"));
    }
}
