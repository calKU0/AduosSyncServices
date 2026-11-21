using Allegro.Aduos.Gaska.ProductsService.Helpers;
using Xunit;

namespace Allegro.Aduos.Gaska.ProductsService.Tests.Helpers;

public class SupplierHtmlTests
{
    [Theory]
    [InlineData("Śruba <M10> ocynkowana", "<p>Śruba &lt;M10&gt; ocynkowana</p>")]
    [InlineData("Złącze <P1> i <A 10>", "<p>Złącze &lt;P1&gt; i &lt;A 10&gt;</p>")]
    [InlineData("Wartość 5 < 10", "<p>Wartość 5 &lt; 10</p>")]
    public void ToBlocks_KeepsBracketedTechnicalTextAsText(string input, string expected)
    {
        Assert.Equal(expected, SupplierHtml.ToBlocks(input));
    }

    [Theory]
    [InlineData("<b>Gwint</b> M10<br/>Długość 50", "<p><b>Gwint</b> M10</p><p>Długość 50</p>")]
    [InlineData("<P>Pierwszy</P><div class=\"x\">Drugi</div>", "<p>Pierwszy</p><p>Drugi</p>")]
    [InlineData("<ul><li>Jeden</li><li>Dwa</li></ul>", "<ul><li>Jeden</li><li>Dwa</li></ul>")]
    [InlineData("<span style=\"color:red\">Tekst</span>", "<p>Tekst</p>")]
    public void ToBlocks_StillConvertsRealMarkup(string input, string expected)
    {
        Assert.Equal(expected, SupplierHtml.ToBlocks(input));
    }
}
