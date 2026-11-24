using AduosSyncServices.Infrastructure.Helpers;
using Xunit;

namespace Allegro.Aduos.Gaska.ProductsService.Tests.Helpers;

public class OfferNameFormatterTests
{
    [Theory]
    [InlineData("ROZRUSZNIK 3.2KW Z REDUKTOREM URSUS C 330", "Rozrusznik 3.2Kw z Reduktorem Ursus C 330")]
    [InlineData("rozrusznik 3.2kw z reduktorem ursus c 330", "Rozrusznik 3.2Kw z Reduktorem Ursus C 330")]
    [InlineData("PRZEGUB KULOWY LEWY ROTA", "Przegub Kulowy Lewy Rota")]
    [InlineData("OBUDOWA ŁOŻYSKA MŁOCARNI", "Obudowa Łożyska Młocarni")]
    [InlineData("TŁUMIK DO URSUS C-360", "Tłumik do Ursus C-360")]
    [InlineData("Z KOMPLETEM ŚRUB", "Z Kompletem Śrub")] // przyimek na początku - wielka litera
    [InlineData("SEKCJA KOSY TYP II", "Sekcja Kosy Typ II")]
    [InlineData("  KOSA   CLAAS  ", "Kosa Claas")]
    [InlineData("JAG59-0030 KOŁO ZĘBATE JAG PREMIUM", "Koło Zębate Jag Premium JAG59-0030")]
    [InlineData("UCFL207 ZESPÓŁ ŁOŻYSKOWY", "Zespół Łożyskowy UCFL207")]
    [InlineData("22.0X8230LI PAS CONTINENTAL C324", "Pas Continental C324 22.0X8230LI")]
    [InlineData("82991684 ROZRUSZNIK 930-170.99", "Rozrusznik 930-170.99 82991684")]
    [InlineData("JAG61-0074/RE520906+ RE523236 FILTR PALIWA - ZESTAW HIFI", "Filtr Paliwa - Zestaw Hifi JAG61-0074/RE520906+ RE523236")]
    [InlineData("3541414M1", "3541414M1")] // sam kod - bez zmian
    [InlineData("150N1280 ORBITROL 150N1280", "Orbitrol 150N1280")]
    [InlineData("ALTERNATOR 14V 50A", "Alternator 14V 50A")]
    public void Format_CapitalizesFirstLetterOfEachWord(string input, string expected)
    {
        Assert.Equal(expected, OfferNameFormatter.Format(input));
    }

    [Fact]
    public void Format_EmptyName_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, OfferNameFormatter.Format(null));
        Assert.Equal(" ", OfferNameFormatter.Format(" "));
    }
}
