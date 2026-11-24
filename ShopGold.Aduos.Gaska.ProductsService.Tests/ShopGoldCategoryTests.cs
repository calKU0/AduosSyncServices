using AduosSyncServices.Contracts.Models;
using ShopGold.Aduos.Gaska.ProductsService.Helpers;
using ShopGold.Aduos.Gaska.ProductsService.Models;
using ShopGold.Aduos.Gaska.ProductsService.Services;
using Xunit;

namespace ShopGold.Aduos.Gaska.ProductsService.Tests
{
    public class ShopGoldCategoryTests
    {
        // Fragmenty odpowiedzi panelu (kategorie_lista.php i podkategorie.php) z HAR.
        private const string TopList =
            "<table class=\"listing_tbl\"><tr class=\"div_naglowek\"> <td class=\"CssId\">ID</td></tr>" +
            "<tr class=\"pozycja_off\" id=\"p_92\"> <td class=\"CssId\">92</td> <td id=\"img_92\" class=\"ImgCursor CssRozwin\"><img src=\"obrazki/rozwin.png\" onclick=\"podkategorie('92',false,'')\" /></td> <td><span class=\"KatGl\">Części do ciągników</span></td></tr>" +
            "<tr class=\"pozycja_off\" id=\"p_91\"> <td class=\"CssId\">91</td> <td id=\"img_91\" class=\"ImgCursor CssRozwin\">-</td> <td><span class=\"KatGl\">Pozostałe części</span></td></tr></table>";

        private const string SubList =
            "<tr class=\"pozycja_off\" id=\"p_123\" data-sciezka=\"92\"> <td class=\"CssId\">123</td> <td><img alt=\"&nbsp;&nbsp;Case / New Holland\" /></td> <td id=\"img_123\"><img onclick=\"podkategorie('123',false,'92_')\" /></td> <td><div style=\"padding-left:25px\"><span class=\"Kateg\">Case / New Holland</span></div></td></tr>" +
            "<tr class=\"pozycja_off\" id=\"p_166\" data-sciezka=\"92\"> <td class=\"CssId\">166</td> <td id=\"img_166\">-</td> <td><div><span class=\"Kateg\">Claas &amp; Renault</span></div></td></tr>";

        [Fact]
        public void CategoryRows_AreParsedFromPanelListing()
        {
            var top = ShopGoldPanelClient.ParseCategoryRows(TopList).ToList();
            Assert.Equal(new[] { (92, "Części do ciągników", true), (91, "Pozostałe części", false) }, top);

            var sub = ShopGoldPanelClient.ParseCategoryRows(SubList).ToList();
            Assert.Equal(new[] { (123, "Case / New Holland", true), (166, "Claas & Renault", false) }, sub);
        }

        [Fact]
        public void CategoryPaths_SkipExcludedAndParentPaths_MainFirst()
        {
            var paths = ShopGoldRowFactory.SelectCategoryPaths(new[]
            {
                new ProductCategory { Name = "Ciągniki > Ursus" },
                new ProductCategory { Name = "Ciągniki > Ursus > C-360" },
                new ProductCategory { Name = "Części według rodzaju > Łożyska > Łożyska kulkowe" },
                new ProductCategory { Name = "Wyprzedaż > Łożyska" }
            }, TestData.Excluded("Wyprzedaż"));

            // Ta sama głębokość - główna jest pierwsza alfabetycznie.
            Assert.Equal(new[] { "Ciągniki > Ursus > C-360", "Części według rodzaju > Łożyska > Łożyska kulkowe" },
                paths.Select(ShopCategoryMap.Key));
        }

        /// <summary>Mapa kategorii sklepu - kategorie nadrzędne dostają id: id liścia * 10 + poziom.</summary>
        internal static ShopCategoryMap Map(params (int Id, string Path)[] categories) =>
            new(categories.SelectMany(c =>
            {
                var segments = c.Path.Split(" > ");
                return Enumerable.Range(1, segments.Length).Select(depth =>
                    new ShopCategory(depth == segments.Length ? c.Id : c.Id * 10 + depth, segments.Take(depth).ToList()));
            }));

        [Fact]
        public void CategoryIds_ListAllShopCategories_WhenAllExist()
        {
            var map = Map((501, "Części według rodzaju > Łożyska > Łożyska kulkowe"), (777, "Części według producentów > Skf"));
            var product = TestData.Product();
            product.Categories!.Add(new ProductCategory { Name = "Ciągniki > Ursus" });

            var withMissing = TestData.Row(product, map: map);
            Assert.False(withMissing.Fields.ContainsKey(ShopGoldFields.CategoryIds));
            Assert.Equal("Ciągniki > Ursus", ShopCategoryMap.Key(Assert.Single(withMissing.MissingCategoryPaths)));

            var complete = TestData.Row(product, map: Map((501, "Części według rodzaju > Łożyska > Łożyska kulkowe"), (88, "Ciągniki > Ursus")));
            Assert.Empty(complete.MissingCategoryPaths);
            // Każdy poziom ścieżki - produkt widać też w kategoriach nadrzędnych.
            // Liść ścieżki głównej pierwszy - sklep bierze pierwsze id jako kategorię główną (ścieżka nawigacji).
            Assert.Equal("501,5012,5011,88,881", complete.Fields[ShopGoldFields.CategoryIds].Columns.Single().Value);
        }

        [Fact]
        public void MissingParentCategory_MakesThePathMissing()
        {
            var onlyLeaf = new ShopCategoryMap(new[]
            {
                new ShopCategory(501, new[] { "Części według rodzaju", "Łożyska", "Łożyska kulkowe" })
            });

            var row = TestData.Row(map: onlyLeaf);

            Assert.False(row.Fields.ContainsKey(ShopGoldFields.CategoryIds));
            Assert.Single(row.MissingCategoryPaths);
        }

        [Fact]
        public void ExcludedBranch_CanBeASubcategory_AndExcludesItsChildren()
        {
            var paths = ShopGoldRowFactory.SelectCategoryPaths(new[]
            {
                new ProductCategory { Name = "Zbiór zbóż i kukurydzy > Kombajny zbożowe > CLAAS > Silnik" },
                new ProductCategory { Name = "Zbiór zbóż i kukurydzy > Prasy" },
                new ProductCategory { Name = "Części według rodzaju > Pompki paliwa" }
            }, TestData.Excluded("ZBIÓR ZBÓŻ I KUKURYDZY > Kombajny zbożowe"));

            Assert.Equal(
                new[] { "Części według rodzaju > Pompki paliwa", "Zbiór zbóż i kukurydzy > Prasy" },
                paths.Select(ShopCategoryMap.Key).OrderBy(p => p, StringComparer.Ordinal));
        }

        [Theory]
        [InlineData("MASSEY FERGUSON", "Massey ferguson")]
        [InlineData("CIĄGNIKI", "Ciągniki")]
        [InlineData("Silnik i osprzęt silnika", "Silnik i osprzęt silnika")]
        [InlineData("ŁOŻYSKA KULKOWE", "Łożyska kulkowe")]
        [InlineData("MF 1150 SERIA", "Mf 1150 seria")]
        [InlineData("URSUS C-360", "Ursus C-360")]
        public void CategoryNames_HaveOnlyFirstLetterCapital(string input, string expected) =>
            Assert.Equal(expected, ShopGoldRowFactory.FormatCategoryName(input));

        [Fact]
        public void CategoryPath_IsFormattedBeforeSendingAndMatching()
        {
            var product = TestData.Product();
            product.Categories = new List<ProductCategory> { new() { Name = "Ciągniki i pojazdy użytkowe > CIĄGNIKI > MASSEY FERGUSON" } };

            var path = Assert.Single(ShopGoldRowFactory.SelectCategoryPaths(product.Categories, Array.Empty<IReadOnlyList<string>>()));

            Assert.Equal("Ciągniki i pojazdy użytkowe > Ciągniki > Massey ferguson", ShopCategoryMap.Key(path));
        }

        [Fact]
        public void CategoryMatching_IgnoresCase_LikeTheShop()
        {
            var map = new ShopCategoryMap(new[] { new ShopCategory(7, new[] { "CIĄGNIKI", "MASSEY FERGUSON" }) });

            Assert.True(map.TryGetId(new[] { "Ciągniki", "Massey ferguson" }, out var id));
            Assert.Equal(7, id);
        }

        [Fact]
        public void NoCategoryMap_MeansNoCategoryIdsAndNothingMissing()
        {
            var row = TestData.Row();
            Assert.False(row.Fields.ContainsKey(ShopGoldFields.CategoryIds));
            Assert.Empty(row.MissingCategoryPaths);
        }

        [Fact]
        public void CategoryNames_AreAddOnly_AndCategoryIdsUpdateOnly()
        {
            Assert.DoesNotContain(ShopGoldFields.CategoryIds, ShopGoldFields.AddFields);
            Assert.Contains(ShopGoldFields.Category, ShopGoldFields.AddFields);

            var sent = ShopGoldSyncPlanner.StateAfterImport(TestData.Row(), ShopGoldFields.AddFields, previous: null);
            var moved = TestData.Product();
            moved.Categories = new List<ProductCategory> { new() { Name = "Ciągniki > Ursus" } };

            var map = Map((88, "Ciągniki > Ursus"));
            var changed = ShopGoldSyncPlanner.ChangedFields(TestData.Row(moved, map: map), sent);

            Assert.Equal(new[] { ShopGoldFields.CategoryIds }, changed);
        }

        [Fact]
        public void WithoutField_ForcesResend()
        {
            var map = Map((501, "Części według rodzaju > Łożyska > Łożyska kulkowe"));
            var row = TestData.Row(map: map);
            var sent = ShopGoldSyncPlanner.StateAfterImport(row, ShopGoldFields.Order, previous: null);

            Assert.Empty(ShopGoldSyncPlanner.ChangedFields(row, sent));
            Assert.Equal(new[] { ShopGoldFields.CategoryIds },
                ShopGoldSyncPlanner.ChangedFields(row, ShopGoldSyncPlanner.WithoutField(sent, ShopGoldFields.CategoryIds)));
        }

        [Fact]
        public void HasAllowedCategory_True_WhenProductIsAlsoInAllowedCategory()
        {
            // Produkt z "Części według producentów" i z "Części według rodzaju" - integrujemy, tylko bez pomijanej gałęzi.
            Assert.True(ShopGoldRowFactory.HasAllowedCategory(TestData.Product(), TestData.Excluded()));
        }

        [Fact]
        public void HasAllowedCategory_False_WhenProductIsOnlyInExcludedCategories()
        {
            var product = TestData.Product();
            product.Categories = new List<ProductCategory>
            {
                new() { Name = "Wyprzedaż > Łożyska" },
                new() { Name = "Części według producentów > SKF > Łożyska" }
            };

            Assert.False(ShopGoldRowFactory.HasAllowedCategory(product, TestData.Excluded()));
        }
    }
}
