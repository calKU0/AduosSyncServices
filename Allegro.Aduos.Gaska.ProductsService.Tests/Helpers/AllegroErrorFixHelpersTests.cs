using AduosSyncServices.Contracts.Models;
using Allegro.Aduos.Gaska.ProductsService.Helpers;
using Allegro.Aduos.Gaska.ProductsService.Services.Allegro;
using Xunit;

namespace Allegro.Aduos.Gaska.ProductsService.Tests.Helpers;

public class AllegroErrorFixHelpersTests
{
    private const string GvoMessage =
        "Parametr zależny Jakość części (zgodnie z GVO) (214434) nie może przyjąć wartości [P - zamiennik o jakości porównywalnej do oryginału], gdy parametr Producent części (127415) ma wartości [Corteco].";

    private static readonly string[] GvoValues =
    {
        "O - oryginał z logo producenta pojazdu",
        "Q - część o jakości oryginału",
        "P - zamiennik o jakości porównywalnej do oryginału",
        "N - zamiennik",
    };

    [Fact]
    public void ExtractFirstBracketValue_ReturnsRejectedGvo()
    {
        Assert.Equal("P - zamiennik o jakości porównywalnej do oryginału", AllegroOfferService.ExtractFirstBracketValue(GvoMessage));
    }

    [Fact]
    public void ExtractLastBracketValues_ReturnsRequiredParameterNames()
    {
        var names = AllegroOfferService.ExtractLastBracketValues(
            "Nie można stworzyć produktu bez podania poprawnych wartości wszystkich parametrów wymaganych: [EAN (GTIN), Kod producenta]");

        Assert.Equal(new[] { "EAN (GTIN)", "Kod producenta" }, names);
    }

    [Fact]
    public void ChooseGvoReplacement_PrefersOriginalQuality()
    {
        Assert.Equal("Q - część o jakości oryginału",
            AllegroParametersService.ChooseGvoReplacement(GvoValues, "P - zamiennik o jakości porównywalnej do oryginału"));
    }

    [Fact]
    public void ChooseGvoReplacement_FallsBackToOriginal_WhenQRejected()
    {
        Assert.Equal("O - oryginał z logo producenta pojazdu",
            AllegroParametersService.ChooseGvoReplacement(GvoValues, "Q - część o jakości oryginału"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("P - zamiennik o jakości porównywalnej do oryginału", true)]
    [InlineData("Q - część o jakości oryginału", false)]
    [InlineData("O - oryginał z logo producenta pojazdu", false)]
    public void IsReplacementQuality(string? value, bool expected)
    {
        Assert.Equal(expected, AllegroParametersService.IsReplacementQuality(value));
    }

    [Theory]
    [InlineData("5901234123457", true)]
    [InlineData("5901234123458", false)]
    [InlineData("96385074", true)]
    [InlineData("12345", false)]
    [InlineData("0000000000000", false)]
    [InlineData("2010000000009", false)] // RCN - prefiks 201, Allegro odrzuca
    [InlineData("0412345678903", false)] // RCN - prefiks 04
    [InlineData("4006381333931", true)]
    [InlineData(null, false)]
    public void IsValidGtin(string? ean, bool expected)
    {
        Assert.Equal(expected, AllegroParametersService.IsValidGtin(ean));
    }

    [Fact]
    public void ResolveParameterValue_MapsEanAndManufacturerCode()
    {
        var product = new Product { Code = "20031744B", Ean = "5901234123457" };

        Assert.Equal("5901234123457", AllegroParametersService.ResolveParameterValue(product, new CategoryParameter { Name = "EAN (GTIN)" }));
        Assert.Equal("20031744B", AllegroParametersService.ResolveParameterValue(product, new CategoryParameter { Name = "Kod producenta" }));
    }

    [Theory]
    [InlineData("SIMMERING 48X72X10", "48X72X10", "Simmering 48X72X10 JAG")]
    [InlineData("SIMMERING", "48X72X10", "Simmering 48X72X10 JAG")]
    [InlineData("48X72X10", "48X72X10", "Część 48X72X10 JAG")]
    [InlineData("ROZRUSZNIK URSUS C 330", "X1", "Rozrusznik Ursus C 330")]
    public void BuildOfferName_HasAtLeastThreeWords(string name, string code, string expected)
    {
        Assert.Equal(expected, OfferFactory.BuildOfferName(new Product { Name = name, Code = code }));
    }
}
