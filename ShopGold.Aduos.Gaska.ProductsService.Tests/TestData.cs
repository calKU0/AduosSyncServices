using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.Settings;
using ShopGold.Aduos.Gaska.ProductsService.Helpers;
using ShopGold.Aduos.Gaska.ProductsService.Models;
using ShopGold.Aduos.Gaska.ProductsService.Settings;

namespace ShopGold.Aduos.Gaska.ProductsService.Tests
{
    internal static class TestData
    {
        public static AppSettings App() => new()
        {
            CategoriesId = new List<int> { 1001 },
            MinProductStock = 1,
            MinProductPriceNet = 1,
            DeliveryMatchMode = AduosSyncServices.Contracts.Data.Enums.DeliveryMatchMode.Price,
            Deliveries = new List<DeliverySettings>
            {
                new() { RuleType = DeliveryRuleType.Standard, HandlingTime = DeliveryHandlingTime.P2D, NetPriceThreshold = 0, DeliveryName = "1,3" },
                new() { RuleType = DeliveryRuleType.BulkyType, HandlingTime = DeliveryHandlingTime.P5D, NetPriceThreshold = 0, DeliveryName = "20" },
                new() { RuleType = DeliveryRuleType.CustomType, HandlingTime = DeliveryHandlingTime.P7D, NetPriceThreshold = 0, DeliveryName = "30" }
            }
        };

        public static ShopGoldSettings Shop() => new() { TaxRate = "23", SafetyInfoPdfUrl = "https://sklep.example.com/images/bezpieczenstwo.pdf" };

        public static FtpSettings Ftp() => new() { ImagesDirectory = "/images/gaska", ImagesCsvPath = "gaska" };

        public static GpsrSettings Gpsr() => new()
        {
            Producer = new GpsrContact { Name = "Przykładowy Sklep sp. z o.o." },
            Importer = new GpsrContact { Name = "Gąska sp. z o.o." }
        };

        public static PriceSettings Prices() => new()
        {
            MarginRanges = new List<MarginRange>
            {
                new() { Min = 0, Max = 25, Margin = 50 },
                new() { Min = 25.01m, Max = 99999, Margin = 40 }
            }
        };

        public static Product Product(string code = "ABC-1", float stock = 5, decimal priceNet = 100m) => new()
        {
            Id = 10,
            Code = code,
            Name = "Łożysko kulkowe 6204",
            Description = "Opis od Gąski",
            Ean = "5900000000001",
            Weight = 0.5f,
            SupplierName = "SKF",
            InStock = stock,
            Unit = "szt",
            PriceNet = priceNet,
            Packages = new List<ProductPackage>(),
            Applications = new List<ProductApplication>(),
            Specifications = new List<ProductSpecification>
            {
                new() { Name = "Średnica", Value = "47", UnitName = "mm" }
            },
            Categories = new List<ProductCategory>
            {
                new() { Name = "Części według rodzaju > Łożyska > Łożyska kulkowe" },
                new() { Name = "Części według producentów > SKF > Łożyska > Kulkowe > Jednorzędowe" }
            }
        };

        public static ShopGoldRow Row(Product? product = null, string[]? images = null, string? imagesHash = "aaaaaaaabbbb", ShopCategoryMap? map = null) =>
            ShopGoldRowFactory.Build(
                product ?? Product(),
                images ?? new[] { @"C:\img\10\image_1.jpg", @"C:\img\10\image_2.png" },
                images is { Length: 0 } ? null : imagesHash,
                App(), Shop(), Ftp(), Gpsr(), Prices(), map, Excluded());

        /// <summary>Pomijane gałęzie jak w domyślnej konfiguracji (ścieżki z drzewa kategorii Gąski).</summary>
        public static List<IReadOnlyList<string>> Excluded(params string[] paths) =>
            (paths.Length > 0 ? paths : new[] { "Koszty transportu", "Wyprzedaż", "Materiały reklamowe", "Części według producentów" })
                .Select(ShopGoldRowFactory.ParseCategoryPath)
                .ToList();
    }
}
