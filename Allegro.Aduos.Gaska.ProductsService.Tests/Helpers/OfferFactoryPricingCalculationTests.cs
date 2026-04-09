using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.Settings;
using Allegro.Aduos.Gaska.ProductsService.Helpers;
using Allegro.Aduos.Gaska.ProductsService.Settings;
using Xunit;

namespace Allegro.Aduos.Gaska.ProductsService.Tests.Helpers;

public class OfferFactoryPricingCalculationTests
{
    [Fact]
    public void BuildOffer_UsesMarginFromMatchingRange_AndPackageQuantity()
    {
        var product = CreateProduct(priceNet: 100m, packageQuantity: 2);
        var priceSettings = CreatePriceSettings(new MarginRange { Min = 0, Max = 200, Margin = 10 });

        var offer = OfferFactory.BuildOffer(product, new List<AllegroCategory>(), CreateAppSettings(), CreateAllegroSettings(), priceSettings);

        Assert.Equal("219.99", offer.SellingMode.Price.Amount);
    }

    [Fact]
    public void BuildOffer_UsesLastMarginRange_WhenPriceDoesNotMatchAnyRange()
    {
        var product = CreateProduct(priceNet: 150m);
        var priceSettings = CreatePriceSettings(
            new MarginRange { Min = 0, Max = 50, Margin = 20 },
            new MarginRange { Min = 51, Max = 100, Margin = 10 });

        var offer = OfferFactory.BuildOffer(product, new List<AllegroCategory>(), CreateAppSettings(), CreateAllegroSettings(), priceSettings);

        Assert.Equal("164.99", offer.SellingMode.Price.Amount);
    }

    [Fact]
    public void BuildOffer_RoundsUpCalculatedPrice_AndSubtractsOneGrosz()
    {
        var product = CreateProduct(priceNet: 99m);
        var priceSettings = CreatePriceSettings(new MarginRange { Min = 0, Max = 200, Margin = 10 });

        var offer = OfferFactory.BuildOffer(product, new List<AllegroCategory>(), CreateAppSettings(), CreateAllegroSettings(), priceSettings);

        Assert.Equal("108.99", offer.SellingMode.Price.Amount);
    }

    private static Product CreateProduct(decimal priceNet, float packageQuantity = 1)
    {
        return new Product
        {
            Code = "TEST-PRICE",
            Name = "Test price product",
            Description = "Description",
            Unit = "szt",
            PriceNet = priceNet,
            InStock = 50,
            DeliveryType = 0,
            BuildCompatibilitySet = false,
            Packages = new List<ProductPackage>
            {
                new()
                {
                    PackRequired = 1,
                    PackQty = packageQuantity,
                    PackUnit = "szt",
                    PackEan = "",
                    PackNettWeight = 0,
                    PackGrossWeight = 0
                }
            },
            Parameters = new List<ProductParameter>(),
            Specifications = new List<ProductSpecification>(),
            AllegroImages = new List<AllegroImages>
            {
                new() { Url = "https://example.com/image-1.jpg" },
                new() { Url = "https://example.com/logo.jpg" }
            }
        };
    }

    private static PriceSettings CreatePriceSettings(params MarginRange[] ranges)
    {
        return new PriceSettings
        {
            MarginRanges = ranges.ToList()
        };
    }

    private static AppSettings CreateAppSettings()
    {
        return new AppSettings
        {
            MinProductStock = 0,
            MinProductPriceNet = 0,
            Deliveries = new List<DeliverySettings>()
        };
    }

    private static AllegroSettings CreateAllegroSettings()
    {
        return new AllegroSettings
        {
            AllegroHandlingTime = "P1D",
            AllegroHandlingTimeCustomProducts = "P2D",
            AllegroSafetyMeasures = "Safe",
            AllegroWarranty = "Warranty",
            AllegroReturnPolicy = "Returns",
            AllegroImpliedWarranty = "Implied",
            AllegroResponsiblePerson = "Person",
            AllegroResponsibleProducer = "Producer"
        };
    }
}
