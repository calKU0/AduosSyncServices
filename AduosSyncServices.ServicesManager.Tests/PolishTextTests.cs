using AduosSyncServices.ServicesManager.Helpers;
using Xunit;

namespace AduosSyncServices.ServicesManager.Tests
{
    public class PolishTextTests
    {
        [Theory]
        [InlineData(1, "zamówienie")]   // singular
        [InlineData(2, "zamówienia")]   // few
        [InlineData(3, "zamówienia")]
        [InlineData(4, "zamówienia")]
        [InlineData(5, "zamówień")]     // many
        [InlineData(0, "zamówień")]
        [InlineData(11, "zamówień")]    // 11-14 are "many" despite ending in 1-4
        [InlineData(12, "zamówień")]
        [InlineData(14, "zamówień")]
        [InlineData(22, "zamówienia")]  // 22 ends in 2 -> few
        [InlineData(25, "zamówień")]
        [InlineData(112, "zamówień")]   // 112 -> many
        [InlineData(122, "zamówienia")] // 122 ends in 22 -> few
        public void Count_AppliesPolishNumeralAgreement(int count, string expected)
            => Assert.Equal(expected, PolishText.Count(count, "zamówienie", "zamówienia", "zamówień"));
    }
}
