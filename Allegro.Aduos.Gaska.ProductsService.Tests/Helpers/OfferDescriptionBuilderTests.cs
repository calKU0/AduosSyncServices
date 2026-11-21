using AduosSyncServices.Contracts.Models;
using Allegro.Aduos.Gaska.ProductsService.Helpers;
using Xunit;

namespace Allegro.Aduos.Gaska.ProductsService.Tests.Helpers;

public class OfferDescriptionBuilderTests
{
    [Fact]
    public void Build_HasNoSeparatorImages_OnlyProductPhotos()
    {
        var product = CreateProduct();
        var images = new List<string> { "img1", "img2", "img3", "img4" };

        var description = OfferDescriptionBuilder.Build(product, images, 1);
        var layout = description.Sections
            .Select(s => string.Join("+", s.SectionItems.Select(i => i.Type == "TEXT" ? "TEXT" : i.Url)))
            .ToList();

        Assert.Equal(new[] { "TEXT", "TEXT+img1", "img2+img3", "img4" }, layout);
    }

    [Fact]
    public void Build_WithoutImages_HasOnlyText()
    {
        var description = OfferDescriptionBuilder.Build(CreateProduct(), new List<string>(), 1);

        Assert.All(description.Sections.SelectMany(s => s.SectionItems), i => Assert.Equal("TEXT", i.Type));
    }

    [Fact]
    public void BuildMainText_ContainsShopStyleSectionsWithEmoji()
    {
        var html = OfferDescriptionBuilder.BuildMainText(CreateProduct(), 2);

        Assert.Contains($"<h2>{OfferDescriptionBuilder.IntroHeader}</h2>", html);
        Assert.Contains($"<h2>{OfferDescriptionBuilder.WhyHeader}</h2>", html);
        Assert.Contains($"<h2>{OfferDescriptionBuilder.SpecificationHeader}</h2>", html);
        Assert.Contains($"<h2>{OfferDescriptionBuilder.ApplicationHeader}</h2>", html);
        Assert.Contains("<p><b>Landini – CIĄGNIKI:</b> Legend 105, Legend 115</p>", html);
        Assert.Contains("<p><b>L - długość:</b> 267 mm</p>", html);
        Assert.Contains("<p><b>F1 - gwint:</b> M20x1,5</p>", html); // bez "mm" przy wartości nieliczbowej
        Assert.DoesNotContain(OfferDescriptionBuilder.PackageHeader, html); // brak opakowania z Gąski
        Assert.DoesNotContain("Producent odpowiedzialny", html);
        Assert.DoesNotContain("🏭", html);
    }

    [Fact]
    public void BuildMainText_EndsWithYellowStarLine()
    {
        var html = OfferDescriptionBuilder.BuildMainText(CreateProduct(), 1);

        Assert.EndsWith("<h2>⭐ Solidna wysokiej jakości renomowana część ⭐</h2>", html);
    }

    [Fact]
    public void BuildMainText_UsesOnlyTagsAllowedByAllegro()
    {
        var html = OfferDescriptionBuilder.BuildMainText(CreateProduct(description: "<section><h1>Nagłówek</h1><p>Akapit <span>x</span></p></section>"), 1);

        var tags = System.Text.RegularExpressions.Regex.Matches(html, @"</?([a-z0-9]+)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        Assert.All(tags, t => Assert.Contains(t, new[] { "h1", "h2", "p", "b", "ul", "ol", "li" }));
        Assert.Contains("<p><b>Nagłówek</b></p>", html);
        Assert.DoesNotContain("&lt;section", html);
    }

    [Fact]
    public void BuildMainText_EncodesSpecialCharacters()
    {
        var html = OfferDescriptionBuilder.BuildMainText(CreateProduct(name: "Śruba <M10> & nakrętka"), 1);

        Assert.Contains("Śruba &lt;M10&gt; &amp; Nakrętka", html);
    }

    [Fact]
    public void BuildMainText_SkipsEmptySections()
    {
        var product = CreateProduct();
        product.Applications = new List<ProductApplication>();
        product.Specifications = new List<ProductSpecification>();
        product.Substitutes = null;
        product.SupplierName = null;

        var html = OfferDescriptionBuilder.BuildMainText(product, 1);

        Assert.DoesNotContain(OfferDescriptionBuilder.ApplicationHeader, html);
        Assert.DoesNotContain(OfferDescriptionBuilder.SpecificationHeader, html);
        Assert.DoesNotContain(OfferDescriptionBuilder.PackageHeader, html);
    }

    [Fact]
    public void BuildMainText_ShowsPackageSection_OnlyForRequiredPackageAboveOne()
    {
        var product = CreateProduct();
        product.Packages = new List<ProductPackage> { new() { PackRequired = 1, PackQty = 4 } };

        var html = OfferDescriptionBuilder.BuildMainText(product, 4);

        Assert.Contains($"<h2>{OfferDescriptionBuilder.PackageHeader}</h2>", html);
        Assert.Contains("Przegub Kulowy – 4 sztuki.", html);

        product.Packages = new List<ProductPackage> { new() { PackRequired = 1, PackQty = 1 } };
        Assert.DoesNotContain(OfferDescriptionBuilder.PackageHeader, OfferDescriptionBuilder.BuildMainText(product, 1));

        product.Packages = new List<ProductPackage> { new() { PackRequired = 0, PackQty = 10 } };
        Assert.DoesNotContain(OfferDescriptionBuilder.PackageHeader, OfferDescriptionBuilder.BuildMainText(product, 1));
    }

    [Fact]
    public void GetOfferImages_CapsGalleryAt16()
    {
        var product = CreateProduct();
        product.AllegroImages = Enumerable.Range(1, 20).Select(i => new AllegroImages { Url = $"img{i}" }).ToList();

        var images = OfferFactory.GetOfferImages(product);

        Assert.Equal(16, images.Count);
        Assert.Equal("img1", images[0]);
        Assert.Equal("img16", images[^1]);
    }

    private static Product CreateProduct(string name = "PRZEGUB KULOWY", string? description = "Opis od dostawcy.")
    {
        return new Product
        {
            Code = "ABC.01",
            Name = name,
            Description = description,
            SupplierName = "ROTA",
            Substitutes = "3541414M1",
            Unit = "szt.",
            Specifications = new List<ProductSpecification>
            {
                new() { Name = "L - długość", Value = "267", UnitName = "mm" },
                new() { Name = "F1 - gwint", Value = "M20x1,5", UnitName = "mm" },
            },
            Applications = new List<ProductApplication>
            {
                new() { ApplicationId = 1, ParentID = 0, Name = "Landini" },
                new() { ApplicationId = 2, ParentID = 1, Name = "CIĄGNIKI" },
                new() { ApplicationId = 3, ParentID = 2, Name = "Legend" },
                new() { ApplicationId = 4, ParentID = 3, Name = "Legend 105" },
                new() { ApplicationId = 5, ParentID = 3, Name = "Legend 115" },
            },
            AllegroImages = new List<AllegroImages>(),
            Packages = new List<ProductPackage>(),
            Parameters = new List<ProductParameter>(),
        };
    }
}
