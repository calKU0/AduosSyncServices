using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Helpers;
using ShopGold.Aduos.Gaska.ProductsService.Helpers;
using ShopGold.Aduos.Gaska.ProductsService.Models;
using Xunit;

namespace ShopGold.Aduos.Gaska.ProductsService.Tests
{
    public class ShopGoldRowFactoryTests
    {
        private static string Value(ShopGoldRow row, string field, string column) =>
            row.Fields[field].Columns.Single(c => c.Key == column).Value;

        [Fact]
        public void Price_IsTheSameAsAllegroPrice()
        {
            var row = TestData.Row();

            var expected = SalesRules.CalculatePrice(100m, 1, TestData.Prices());
            Assert.Equal(expected.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), Value(row, ShopGoldFields.Price, "Cena_brutto"));
            Assert.Equal("139.99", Value(row, ShopGoldFields.Price, "Cena_brutto"));
        }

        [Fact]
        public void PackageProduct_IsSoldAsSet_WithPackagePriceAndStock()
        {
            var product = TestData.Product(stock: 25, priceNet: 10m);
            product.Packages = new List<ProductPackage> { new() { PackRequired = 1, PackQty = 10, PackUnit = "szt" } };

            var row = TestData.Row(product);

            Assert.Equal("2", Value(row, ShopGoldFields.Stock, "Ilosc_produktow"));
            Assert.Equal("kpl.", Value(row, ShopGoldFields.Unit, "Jednostka_miary"));
            Assert.Equal("5.0000", Value(row, ShopGoldFields.Weight, "Waga"));
            Assert.Equal(SalesRules.CalculatePrice(10m, 10, TestData.Prices()).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                Value(row, ShopGoldFields.Price, "Cena_brutto"));
        }

        [Fact]
        public void ReferenceNumbers_PurchasePrice_ExternalId_AndSafetyLink_AreSent()
        {
            var product = TestData.Product(priceNet: 12.5m);
            product.Substitutes = "AR1, AR2 ,AR1, AR3, AR4, AR5, AR6";
            product.Packages = new List<ProductPackage> { new() { PackRequired = 1, PackQty = 4, PackUnit = "szt" } };

            var row = TestData.Row(product);

            Assert.Equal(new[] { "AR1", "AR2", "AR3", "AR4", "AR5" },
                Enumerable.Range(1, 5).Select(i => Value(row, ShopGoldFields.ReferenceNumbers, $"Nr_referencyjny_{i}")));
            Assert.Equal("50.00", Value(row, ShopGoldFields.PurchasePrice, "Cena_zakupu"));   // 12,50 netto x 4 szt. w opakowaniu
            Assert.Equal("10", Value(row, ShopGoldFields.ExternalId, "Id_produktu_magazyn"));
            Assert.StartsWith("https://sklep.example.com/images/", Value(row, ShopGoldFields.Gpsr, "Informacja_o_bezpieczenstwie"));

            var noSubstitutes = TestData.Row();
            Assert.All(Enumerable.Range(1, 5), i => Assert.Equal(string.Empty, Value(noSubstitutes, ShopGoldFields.ReferenceNumbers, $"Nr_referencyjny_{i}")));
        }

        [Fact]
        public void AdditionalFields_ContainSpecificationAndSubstitutes()
        {
            var product = TestData.Product();
            product.Substitutes = string.Join(",", Enumerable.Range(1, 60).Select(i => $"NR{i:000}-ABCDEF"));

            var row = TestData.Row(product);

            Assert.Equal("Średnica", Value(row, ShopGoldFields.AdditionalFields, "Dodatkowe_pole_1_nazwa"));
            Assert.Equal("47 mm", Value(row, ShopGoldFields.AdditionalFields, "Dodatkowe_pole_1_wartosc"));
            Assert.Equal("Numery zamienników", Value(row, ShopGoldFields.AdditionalFields, "Dodatkowe_pole_2_nazwa"));

            var first = Value(row, ShopGoldFields.AdditionalFields, "Dodatkowe_pole_2_wartosc");
            var second = Value(row, ShopGoldFields.AdditionalFields, "Dodatkowe_pole_2_wartosc_2");
            Assert.StartsWith("NR001-ABCDEF, NR002-ABCDEF", first);
            Assert.True(first.Length <= 250 && second.Length <= 250);
            Assert.StartsWith("NR", second);
            Assert.Equal(string.Empty, Value(row, ShopGoldFields.AdditionalFields, "Dodatkowe_pole_3_nazwa"));
            Assert.Equal(ShopGoldRowFactory.MaxAdditionalFields * 4, row.Fields[ShopGoldFields.AdditionalFields].Columns.Count);
        }

        [Fact]
        public void Name_IsFormattedLikeOnAllegro_CatalogCodesAtTheEnd()
        {
            var product = TestData.Product();
            product.Name = "JAG08-0091 POMPKA ZASILAJĄCA TŁOCZKOWA";

            var row = TestData.Row(product);

            Assert.Equal("Pompka Zasilająca Tłoczkowa JAG08-0091", Value(row, ShopGoldFields.Name, "Nazwa_produktu"));
            Assert.Contains("<b>Pompka Zasilająca Tłoczkowa JAG08-0091</b>", Value(row, ShopGoldFields.Description, "Opis"));
        }

        [Fact]
        public void Category_PicksDeepestPathOutsideExcludedRoots()
        {
            var row = TestData.Row();

            Assert.Equal("Części według rodzaju", Value(row, ShopGoldFields.Category, "Kategoria_1_nazwa"));
            Assert.Equal("Łożyska", Value(row, ShopGoldFields.Category, "Kategoria_2_nazwa"));
            Assert.Equal("Łożyska kulkowe", Value(row, ShopGoldFields.Category, "Kategoria_3_nazwa"));
            Assert.Equal(string.Empty, Value(row, ShopGoldFields.Category, "Kategoria_4_nazwa"));
        }

        [Fact]
        public void Category_IsEmpty_WhenOnlyExcludedCategories()
        {
            var paths = ShopGoldRowFactory.SelectCategoryPaths(
                new[] { new ProductCategory { Name = "Wyprzedaż > Łożyska" } },
                TestData.Excluded("Wyprzedaż"));

            Assert.Empty(paths);

            var product = TestData.Product();
            product.Categories = new List<ProductCategory> { new() { Name = "Wyprzedaż > Łożyska" } };
            Assert.Equal(string.Empty, Value(TestData.Row(product), ShopGoldFields.Category, "Kategoria_1_nazwa"));
        }

        [Fact]
        public void PackagedProduct_WithoutFullPackage_IsInactive()
        {
            // 5 szt. na stanie, Gąska sprzedaje po 10 - nie ma czego sprzedać.
            var product = TestData.Product(stock: 5);
            product.Packages = new List<ProductPackage> { new() { PackRequired = 1, PackQty = 10, PackUnit = "szt" } };

            var row = TestData.Row(product);

            Assert.False(row.IsActive);
            Assert.Equal("0", Value(row, ShopGoldFields.Stock, "Ilosc_produktow"));
            Assert.Equal("nie", Value(row, ShopGoldFields.Status, "Status"));
        }

        [Theory]
        [InlineData("ABC-1", "ABC-1")]
        [InlineData("AB_1", "AB_1")]
        public void ImageFolder_KeepsSafeCodes(string code, string expected) =>
            Assert.Equal(expected, ShopGoldRowFactory.ImageFolder(code));

        [Fact]
        public void ImageFolder_DoesNotCollide_ForCodesDifferingOnlyBySpecialCharacters()
        {
            var slash = ShopGoldRowFactory.ImageFolder("A/B");
            var dot = ShopGoldRowFactory.ImageFolder("A.B");

            Assert.StartsWith("A_B_", slash);
            Assert.NotEqual(slash, dot);
            Assert.NotEqual("A_B", slash);
        }

        [Fact]
        public void Delivery_UsesShippingIdsHandlingTimeAndBulkyFlag()
        {
            var standard = TestData.Row();
            Assert.Equal("1,3", Value(standard, ShopGoldFields.Shipping, "Wysylki_id"));
            Assert.Equal("nie", Value(standard, ShopGoldFields.Shipping, "Gabaryt"));
            Assert.Equal("48 godzin", Value(standard, ShopGoldFields.ShippingTime, "Termin_wysylki"));

            var bulkyProduct = TestData.Product();
            bulkyProduct.DeliveryType = 1;
            var bulky = TestData.Row(bulkyProduct);
            Assert.Equal("20", Value(bulky, ShopGoldFields.Shipping, "Wysylki_id"));
            Assert.Equal("tak", Value(bulky, ShopGoldFields.Shipping, "Gabaryt"));
            Assert.Equal("5 dni", Value(bulky, ShopGoldFields.ShippingTime, "Termin_wysylki"));
        }

        [Fact]
        public void PalletDelivery_UsesPalletShippingAndIsBulky()
        {
            var palletProduct = TestData.Product();
            palletProduct.DeliveryType = 2;
            var pallet = TestData.Row(palletProduct);

            Assert.Equal("30", Value(pallet, ShopGoldFields.Shipping, "Wysylki_id"));
            Assert.Equal("tak", Value(pallet, ShopGoldFields.Shipping, "Gabaryt"));
            Assert.Equal("10 dni", Value(pallet, ShopGoldFields.ShippingTime, "Termin_wysylki"));
        }

        [Theory]
        [InlineData(DeliveryHandlingTime.PT0S, "24 godziny")]
        [InlineData(DeliveryHandlingTime.PT24H, "24 godziny")]
        [InlineData(DeliveryHandlingTime.P2D, "48 godzin")]
        [InlineData(DeliveryHandlingTime.P3D, "3 dni")]
        [InlineData(DeliveryHandlingTime.P5D, "5 dni")]
        [InlineData(DeliveryHandlingTime.P7D, "10 dni")]
        [InlineData(DeliveryHandlingTime.P14D, "14 dni")]
        [InlineData(DeliveryHandlingTime.P30D, "14 dni")]
        public void HandlingTime_MapsToShopDictionary(DeliveryHandlingTime time, string expected) =>
            Assert.Equal(expected, ShopGoldRowFactory.HandlingTimeName(time));

        [Fact]
        public void Status_IsInactive_BelowMinimumStock()
        {
            var row = TestData.Row(TestData.Product(stock: 0));

            Assert.False(row.IsActive);
            Assert.Equal("nie", Value(row, ShopGoldFields.Status, "Status"));
        }

        [Fact]
        public void Images_UseProductFolderAndContentHashInFileNames()
        {
            var row = TestData.Row();

            Assert.Equal("gaska/ABC-1/1_aaaaaaaa.jpg", Value(row, ShopGoldFields.Images, "Zdjecie_glowne"));
            Assert.Equal("gaska/ABC-1/2_aaaaaaaa.png", Value(row, ShopGoldFields.Images, "Zdjecie_dodatkowe_1"));
            Assert.Equal(string.Empty, Value(row, ShopGoldFields.Images, "Zdjecie_dodatkowe_2"));
            Assert.Equal("ABC-1/1_aaaaaaaa.jpg", row.Images[0].RemoteFileName);
        }

        [Theory]
        [InlineData("1, 3", "1,3")]
        [InlineData("2;5", "2,5")]
        [InlineData("JAG API", "")]
        [InlineData(null, "")]
        public void ShippingIds_AreNormalized(string? input, string expected) =>
            Assert.Equal(expected, ShopGoldRowFactory.NormalizeShippingIds(input));

        [Fact]
        public void Description_FollowsShopLayout()
        {
            var html = Value(TestData.Row(), ShopGoldFields.Description, "Opis");

            Assert.Contains($"<h3>{ShopGoldDescriptionBuilder.IntroHeader}</h3>", html);
            Assert.Contains("<p>Opis od Gąski</p>", html);
            Assert.Contains($"<h3>{ShopGoldDescriptionBuilder.WhyHeader}</h3><ul><li><b>Wysoka jakość:</b> zamiennik renomowanej firmy SKF.</li>", html);
            Assert.Contains($"<h3>{ShopGoldDescriptionBuilder.SpecificationHeader}</h3><p><b>Producent:</b> SKF</p><p><b>Numer katalogowy:</b> ABC-1</p>", html);
            Assert.Contains("<p><b>Średnica:</b> 47 mm</p>", html);
            Assert.DoesNotContain("Wysokiej jakości zamiennik renomowanej firmy", html);
            Assert.Contains($"<h3>{ShopGoldDescriptionBuilder.SafetyHeader}</h3>", html);
            Assert.Contains(">Producent odpowiedzialny</th><td style=\"border: 1px solid #dbdbdb; padding: 8px 14px; text-align: left;\">Przykładowy Sklep sp. z o.o.</td>", html);
            Assert.Contains(">Osoba odpowiedzialna za zgodność produktu</th><td style=\"border: 1px solid #dbdbdb; padding: 8px 14px; text-align: left;\">Gąska sp. z o.o.</td>", html);
            Assert.EndsWith("&#128196; Informacje o bezpieczeństwie produktu (PDF)</a></p>", html);
            Assert.Contains("href=\"https://sklep.example.com/images/bezpieczenstwo.pdf\"", html);
            Assert.DoesNotContain(ShopGoldDescriptionBuilder.PackageHeader, html);
            Assert.DoesNotContain(ShopGoldDescriptionBuilder.ApplicationHeader, html);
            Assert.DoesNotContain("\n", html);

            // Emotki jako encje HTML - bez 4-bajtowych znaków UTF-8.
            Assert.DoesNotContain(html, c => char.IsSurrogate(c));
        }

        [Fact]
        public void Description_HasPackageSection_OnlyForPackagedProducts()
        {
            var product = TestData.Product(stock: 25, priceNet: 10m);
            product.Packages = new List<ProductPackage> { new() { PackRequired = 1, PackQty = 10, PackUnit = "szt" } };

            var html = Value(TestData.Row(product), ShopGoldFields.Description, "Opis");

            Assert.Contains($"<h3>{ShopGoldDescriptionBuilder.PackageHeader}</h3><p>10 sztuk – Łożysko Kulkowe 6204.</p>", html);
            Assert.Contains("<ul><li><b>Ilość w zestawie:</b> 10 sztuk</li></ul>", html);
        }

        [Fact]
        public void Description_ListsApplications()
        {
            var product = TestData.Product();
            product.Applications = new List<ProductApplication>
            {
                new() { ApplicationId = 1, ParentID = 0, Name = "John Deere" },
                new() { ApplicationId = 2, ParentID = 1, Name = "Ciągniki" },
                new() { ApplicationId = 3, ParentID = 2, Name = "2140" },
                new() { ApplicationId = 4, ParentID = 2, Name = "3650" }
            };

            var html = Value(TestData.Row(product), ShopGoldFields.Description, "Opis");

            Assert.Contains("do maszyn marki <b>John Deere</b>", html);
            Assert.Contains($"<h3>{ShopGoldDescriptionBuilder.ApplicationHeader}</h3><p>Część pasuje do maszyn:</p><p><b>John Deere – Ciągniki:</b> 2140, 3650</p>", html);
        }

        [Fact]
        public void SupplierDescription_DecodesEscapedHtmlAndDropsScripts()
        {
            Assert.Equal("<p>A</p>", ShopGoldDescriptionBuilder.SupplierDescription("&lt;p&gt;A&lt;/p&gt;"));
            Assert.Equal("<p>B</p>", ShopGoldDescriptionBuilder.SupplierDescription("<p>B</p><script>alert(1)</script>"));
        }
    }
}
