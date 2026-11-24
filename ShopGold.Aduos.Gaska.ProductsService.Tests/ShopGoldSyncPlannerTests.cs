using ShopGold.Aduos.Gaska.ProductsService.Helpers;
using ShopGold.Aduos.Gaska.ProductsService.Models;
using Xunit;

namespace ShopGold.Aduos.Gaska.ProductsService.Tests
{
    public class ShopGoldSyncPlannerTests
    {
        private static Dictionary<string, ShopGoldProductState> States(params ShopGoldProductState[] states) =>
            states.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

        private static ShopGoldProductState SentState(ShopGoldRow row) =>
            ShopGoldSyncPlanner.StateAfterImport(row, ShopGoldFields.Order, previous: null);

        private static ShopGoldSyncPlan Plan(IReadOnlyCollection<ShopGoldRow> rows, Dictionary<string, ShopGoldProductState> states, int maxNew = 100, bool deactivate = true) =>
            ShopGoldSyncPlanner.Plan(rows, states, maxNew, skipProductsWithoutImages: true, allowDeactivation: deactivate);

        [Fact]
        public void NewProduct_IsAddedWithAllFields()
        {
            var plan = Plan(new[] { TestData.Row() }, States());

            Assert.Single(plan.Adds);
            Assert.Empty(plan.Updates);
        }

        [Fact]
        public void UnchangedProduct_IsNotSent()
        {
            var row = TestData.Row();

            var plan = Plan(new[] { TestData.Row() }, States(SentState(row)));

            Assert.Empty(plan.Adds);
            Assert.Empty(plan.Updates);
            Assert.Equal(1, plan.Unchanged);
        }

        [Fact]
        public void PriceAndStockChange_SendsOnlyThoseFields()
        {
            var sent = SentState(TestData.Row());

            var plan = Plan(new[] { TestData.Row(TestData.Product(stock: 9, priceNet: 120m)) }, States(sent));

            var batch = Assert.Single(plan.Updates);
            Assert.Equal(new[] { ShopGoldFields.Price, ShopGoldFields.PurchasePrice, ShopGoldFields.Stock }, batch.Fields);
            Assert.Single(batch.Rows);
        }

        [Fact]
        public void ChangedImages_AreSent_MissingLocalImagesAreNot()
        {
            var sent = SentState(TestData.Row());

            var changed = Plan(new[] { TestData.Row(imagesHash: "cccccccc1111") }, States(sent));
            Assert.Equal(new[] { ShopGoldFields.Images }, Assert.Single(changed.Updates).Fields);

            var missing = Plan(new[] { TestData.Row(images: Array.Empty<string>()) }, States(sent));
            Assert.Empty(missing.Updates);
        }

        [Fact]
        public void ProductGoneFromCatalog_IsDeactivated_UnlessDeactivationDisabled()
        {
            var sent = SentState(TestData.Row());
            var other = TestData.Row(TestData.Product(code: "XYZ"));

            var plan = Plan(new[] { other }, States(sent));
            var deactivation = Assert.Single(plan.Updates);
            Assert.Equal(new[] { ShopGoldFields.Status }, deactivation.Fields);
            Assert.Equal("ABC-1", Assert.Single(deactivation.Rows).CatalogNumber);
            Assert.False(deactivation.Rows[0].IsActive);

            var guarded = Plan(new[] { other }, States(sent), deactivate: false);
            Assert.Empty(guarded.Updates);
        }

        [Fact]
        public void NewProducts_WithoutStockOrImages_AreSkipped_AndAddsAreCapped()
        {
            var rows = new[]
            {
                TestData.Row(TestData.Product(code: "A", stock: 0)),
                TestData.Row(TestData.Product(code: "B"), images: Array.Empty<string>()),
                TestData.Row(TestData.Product(code: "C")),
                TestData.Row(TestData.Product(code: "D"))
            };

            var plan = Plan(rows, States(), maxNew: 1);

            Assert.Equal("C", Assert.Single(plan.Adds).CatalogNumber);
            Assert.Equal(1, plan.SkippedInactiveNew);
            Assert.Equal(1, plan.SkippedWithoutImages);
            Assert.Equal(1, plan.DeferredNew);
        }

        [Fact]
        public void StateAfterUpdate_KeepsPreviousFingerprintsAndImagesHash()
        {
            var sent = SentState(TestData.Row());
            var changedRow = TestData.Row(TestData.Product(priceNet: 150m), imagesHash: "zzzz");

            var state = ShopGoldSyncPlanner.StateAfterImport(changedRow, new[] { ShopGoldFields.Price }, sent);

            var before = ShopGoldSyncPlanner.ParseFingerprints(sent.FieldsJson);
            var after = ShopGoldSyncPlanner.ParseFingerprints(state.FieldsJson);
            Assert.NotEqual(before[ShopGoldFields.Price], after[ShopGoldFields.Price]);
            Assert.Equal(before[ShopGoldFields.Images], after[ShopGoldFields.Images]);
            Assert.Equal(sent.ImagesHash, state.ImagesHash);
        }

        [Fact]
        public void RowsNotAdded_AreThoseMissingFromAddedNames()
        {
            var a = TestData.Product(code: "A"); a.Name = "Łożysko A";
            var b = TestData.Product(code: "B"); b.Name = "Łożysko B";
            var c = TestData.Product(code: "C"); c.Name = "Łożysko  B";   // ta sama nazwa co B (po normalizacji)
            var rows = new[] { TestData.Row(a), TestData.Row(b), TestData.Row(c) };

            // Sklep dodał A i jedno "Łożysko B" - drugie o tej nazwie już było w sklepie.
            var notAdded = ShopGoldSyncPlanner.RowsNotAdded(rows, 2, new[] { "łożysko a", "Łożysko B" });

            Assert.Equal("C", Assert.Single(notAdded).CatalogNumber);
        }

        [Fact]
        public void RowsNotAdded_IsEmpty_WhenEverythingWasAdded() =>
            Assert.Empty(ShopGoldSyncPlanner.RowsNotAdded(new[] { TestData.Row() }, 1, Array.Empty<string>()));

        [Fact]
        public void RowsNotAdded_IsEverything_WhenNothingWasAdded()
        {
            var rows = new[] { TestData.Row(TestData.Product(code: "A")), TestData.Row(TestData.Product(code: "B")) };
            Assert.Equal(2, ShopGoldSyncPlanner.RowsNotAdded(rows, 0, Array.Empty<string>()).Count);
        }

        [Fact]
        public void AddedNames_AreReadFromImportResponse() =>
            Assert.Equal(
                new[] { "Chłodnica miedziana Perkins", "Pas & klin" },
                ShopGold.Aduos.Gaska.ProductsService.Services.ShopGoldPanelClient.ParseNames("<li>Chłodnica miedziana Perkins</li><li>Pas &amp; klin</li>"));

        [Fact]
        public void DifferentChanges_GoToOneCombinedFile_WithCurrentValuesForUnchangedFields()
        {
            var states = new Dictionary<string, ShopGoldProductState>(StringComparer.OrdinalIgnoreCase);
            var rows = new List<ShopGoldRow>();

            // Każdy produkt ma inny zestaw zmian: cena, stan, waga, nazwa... w różnych kombinacjach.
            for (var i = 0; i < 12; i++)
            {
                var product = TestData.Product(code: "P" + i, stock: 5 + (i & 1), priceNet: 100m + (i & 2));
                product.Weight = 0.5f + (i & 4);
                product.Name = (i & 8) != 0 ? "Inna nazwa" : product.Name;
                states["P" + i] = SentState(TestData.Row(TestData.Product(code: "P" + i)));
                rows.Add(TestData.Row(product));
            }

            var plan = Plan(rows, states);

            var batch = Assert.Single(plan.Updates);
            Assert.Equal(11, batch.Rows.Count);
            Assert.Equal(new[] { ShopGoldFields.Name, ShopGoldFields.Price, ShopGoldFields.PurchasePrice, ShopGoldFields.Stock, ShopGoldFields.Weight, ShopGoldFields.Description },
                batch.Fields);

            // Produkt, w którym zmienił się tylko stan, wysyła też obecną cenę i wagę.
            var csv = ShopGoldCsvWriter.Write(batch.Rows, batch.Fields);
            Assert.Contains("\"P1\";\"Łożysko Kulkowe 6204\";\"139.99\";\"23\";\"100.00\";\"6\";\"0.5000\"", csv);
        }

        [Fact]
        public void ImagesAndRowsWithoutSomeColumn_GoToSeparateFiles()
        {
            var map = ShopGoldCategoryTests.Map((501, "Części według rodzaju > Łożyska > Łożyska kulkowe"));

            // A: zmiana ceny, zdjęć i (nowe) Kategorie_id. B: zmiana stanu, ale jego kategorii nie ma w sklepie.
            var a = TestData.Row(TestData.Product(code: "A", priceNet: 120m), imagesHash: "cccccccc1111", map: map);
            var bProduct = TestData.Product(code: "B", stock: 9);
            bProduct.Categories = new List<AduosSyncServices.Contracts.Models.ProductCategory> { new() { Name = "Ciągniki > Ursus" } };
            var b = TestData.Row(bProduct, map: map);

            var states = States(SentState(TestData.Row(TestData.Product(code: "A"))), SentState(TestData.Row(TestData.Product(code: "B"))));
            var plan = Plan(new[] { a, b }, states);

            Assert.Equal(3, plan.Updates.Count);
            Assert.Equal(new[] { ShopGoldFields.CategoryIds, ShopGoldFields.Price, ShopGoldFields.PurchasePrice, ShopGoldFields.Stock }, plan.Updates[0].Fields);
            Assert.Equal("A", Assert.Single(plan.Updates[0].Rows).CatalogNumber);
            Assert.Equal(new[] { ShopGoldFields.Price, ShopGoldFields.PurchasePrice, ShopGoldFields.Stock }, plan.Updates[1].Fields);
            Assert.Equal("B", Assert.Single(plan.Updates[1].Rows).CatalogNumber);
            Assert.Equal(new[] { ShopGoldFields.Images }, plan.Updates[2].Fields);
            Assert.Equal("A", Assert.Single(plan.Updates[2].Rows).CatalogNumber);
        }
    }
}
