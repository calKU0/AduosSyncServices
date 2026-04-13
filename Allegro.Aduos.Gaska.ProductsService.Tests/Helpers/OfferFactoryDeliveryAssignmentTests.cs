using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.Settings;
using Allegro.Aduos.Gaska.ProductsService.Helpers;
using Xunit;

namespace Allegro.Aduos.Gaska.ProductsService.Tests.Helpers;

public class OfferFactoryDeliveryAssignmentTests
{
    [Fact]
    public void ResolveDeliveryNameForTests_ReturnsCustomRate_ForDeliveryType2()
    {
        var product = CreateProduct(deliveryType: 2, weight: 12, length: 40, width: 20, height: 10);
        var deliveries = new List<DeliverySettings>
        {
            new() { RuleType = DeliveryRuleType.CustomType, DeliveryName = "CUSTOM", HandlingTime = DeliveryHandlingTime.PT2D },
            new() { RuleType = DeliveryRuleType.Standard, NetPriceThreshold = 0, DeliveryName = "STANDARD" },
            new() { RuleType = DeliveryRuleType.BulkyType, NetPriceThreshold = 0, DeliveryName = "BULKY" }
        };

        var result = OfferFactory.ResolveDeliveryNameForTests(product, deliveries, product.PriceNet, DeliveryMatchMode.Price);

        Assert.Equal("CUSTOM", result);
    }

    [Fact]
    public void ResolveHandlingTimeForTests_ReturnsHandlingTime_FromMatchedDeliveryRule()
    {
        var product = CreateProduct(deliveryType: 0, weight: 8, length: 30, width: 20, height: 10, priceNet: 850);
        var deliveries = new List<DeliverySettings>
        {
            new() { RuleType = DeliveryRuleType.CustomType, DeliveryName = "CUSTOM", HandlingTime = DeliveryHandlingTime.PT5D },
            new() { RuleType = DeliveryRuleType.BulkyType, NetPriceThreshold = 0, DeliveryName = "BULKY", HandlingTime = DeliveryHandlingTime.P14D },
            new() { RuleType = DeliveryRuleType.Standard, NetPriceThreshold = 0, DeliveryName = "STANDARD-0", HandlingTime = DeliveryHandlingTime.PT24H },
            new() { RuleType = DeliveryRuleType.Standard, NetPriceThreshold = 800, DeliveryName = "STANDARD-800", HandlingTime = DeliveryHandlingTime.PT3D }
        };

        var result = OfferFactory.ResolveHandlingTimeForTests(product, deliveries, product.PriceNet, DeliveryMatchMode.Price);

        Assert.Equal("PT3D", result);
    }

    [Fact]
    public void ResolveDeliveryNameForTests_ReturnsBulkyRate_ForDeliveryType1_InPriceMode()
    {
        var product = CreateProduct(deliveryType: 1, weight: 12, length: 40, width: 20, height: 10, priceNet: 1400);
        var deliveries = new List<DeliverySettings>
        {
            new() { RuleType = DeliveryRuleType.CustomType, DeliveryName = "CUSTOM" },
            new() { RuleType = DeliveryRuleType.BulkyType, NetPriceThreshold = 0, DeliveryName = "BULKY-0" },
            new() { RuleType = DeliveryRuleType.BulkyType, NetPriceThreshold = 1300, DeliveryName = "BULKY-1300" },
            new() { RuleType = DeliveryRuleType.Standard, NetPriceThreshold = 0, DeliveryName = "STANDARD" }
        };

        var result = OfferFactory.ResolveDeliveryNameForTests(product, deliveries, product.PriceNet, DeliveryMatchMode.Price);

        Assert.Equal("BULKY-1300", result);
    }

    [Fact]
    public void ResolveDeliveryNameForTests_ReturnsStandardRate_ForRegularProduct_InPriceMode()
    {
        var product = CreateProduct(deliveryType: 0, weight: 8, length: 30, width: 20, height: 10, priceNet: 850);
        var deliveries = new List<DeliverySettings>
        {
            new() { RuleType = DeliveryRuleType.CustomType, DeliveryName = "CUSTOM" },
            new() { RuleType = DeliveryRuleType.BulkyType, NetPriceThreshold = 0, DeliveryName = "BULKY" },
            new() { RuleType = DeliveryRuleType.Standard, NetPriceThreshold = 0, DeliveryName = "STANDARD-0" },
            new() { RuleType = DeliveryRuleType.Standard, NetPriceThreshold = 800, DeliveryName = "STANDARD-800" }
        };

        var result = OfferFactory.ResolveDeliveryNameForTests(product, deliveries, product.PriceNet, DeliveryMatchMode.Price);

        Assert.Equal("STANDARD-800", result);
    }

    [Fact]
    public void ResolveDeliveryNameForTests_ReturnsSmallestMatchingStandardRate_InDimensionsMode()
    {
        var product = CreateProduct(deliveryType: 0, weight: 10, length: 50, width: 30, height: 20);
        var deliveries = new List<DeliverySettings>
        {
            new() { RuleType = DeliveryRuleType.CustomType, DeliveryName = "CUSTOM" },
            new() { RuleType = DeliveryRuleType.BulkyType, Weight = 999, Length = 999, Width = 999, Height = 999, DeliveryName = "BULKY" },
            new() { RuleType = DeliveryRuleType.Standard, Weight = 50, Length = 120, Width = 80, Height = 60, DeliveryName = "STANDARD-BIG" },
            new() { RuleType = DeliveryRuleType.Standard, Weight = 15, Length = 60, Width = 40, Height = 30, DeliveryName = "STANDARD-SMALL" }
        };

        var result = OfferFactory.ResolveDeliveryNameForTests(product, deliveries, product.PriceNet, DeliveryMatchMode.Weight);

        Assert.Equal("STANDARD-SMALL", result);
    }

    [Fact]
    public void ResolveDeliveryNameForTests_ReturnsSmallestMatchingBulkyRate_InDimensionsMode()
    {
        var product = CreateProduct(deliveryType: 1, weight: 80, length: 180, width: 90, height: 70);
        var deliveries = new List<DeliverySettings>
        {
            new() { RuleType = DeliveryRuleType.CustomType, DeliveryName = "CUSTOM" },
            new() { RuleType = DeliveryRuleType.BulkyType, Weight = 999, Length = 999, Width = 999, Height = 999, DeliveryName = "BULKY-BIG" },
            new() { RuleType = DeliveryRuleType.BulkyType, Weight = 100, Length = 200, Width = 100, Height = 80, DeliveryName = "BULKY-SMALL" },
            new() { RuleType = DeliveryRuleType.Standard, Weight = 50, Length = 120, Width = 80, Height = 60, DeliveryName = "STANDARD" }
        };

        var result = OfferFactory.ResolveDeliveryNameForTests(product, deliveries, product.PriceNet, DeliveryMatchMode.Weight);

        Assert.Equal("BULKY-SMALL", result);
    }

    [Fact]
    public void ResolveDeliveryNameForTests_UsesLargestRuleAsFallback_WhenNoDimensionsRuleMatches()
    {
        var product = CreateProduct(deliveryType: 0, weight: 100, length: 300, width: 200, height: 150);
        var deliveries = new List<DeliverySettings>
        {
            new() { RuleType = DeliveryRuleType.CustomType, DeliveryName = "CUSTOM" },
            new() { RuleType = DeliveryRuleType.BulkyType, Weight = 999, Length = 999, Width = 999, Height = 999, DeliveryName = "BULKY" },
            new() { RuleType = DeliveryRuleType.Standard, Weight = 30, Length = 80, Width = 50, Height = 40, DeliveryName = "STANDARD-SMALL" },
            new() { RuleType = DeliveryRuleType.Standard, Weight = 60, Length = 120, Width = 90, Height = 70, DeliveryName = "STANDARD-BIG" }
        };

        var result = OfferFactory.ResolveDeliveryNameForTests(product, deliveries, product.PriceNet, DeliveryMatchMode.Weight);

        Assert.Equal("STANDARD-BIG", result);
    }

    [Fact]
    public void ResolveDeliveryNameForTests_ReturnsNull_WhenNoDeliveriesProvided()
    {
        var product = CreateProduct(deliveryType: 0, weight: 10, length: 50, width: 30, height: 20);

        var result = OfferFactory.ResolveDeliveryNameForTests(product, new List<DeliverySettings>(), product.PriceNet, DeliveryMatchMode.Price);

        Assert.Null(result);
    }

    private static Product CreateProduct(int deliveryType, float weight, decimal length, decimal width, decimal height, decimal priceNet = 100)
    {
        return new Product
        {
            Code = "TEST",
            Name = "Test product",
            Unit = "szt",
            DeliveryType = deliveryType,
            Weight = weight,
            PriceNet = priceNet,
            Specifications = new List<ProductSpecification>
            {
                new() { Name = "Długość", Value = length.ToString(System.Globalization.CultureInfo.InvariantCulture), UnitName = "cm" },
                new() { Name = "Szerokość", Value = width.ToString(System.Globalization.CultureInfo.InvariantCulture), UnitName = "cm" },
                new() { Name = "Wysokość", Value = height.ToString(System.Globalization.CultureInfo.InvariantCulture), UnitName = "cm" }
            }
        };
    }
}
