using ShopGold.Aduos.Gaska.ProductsService.Helpers;
using ShopGold.Aduos.Gaska.ProductsService.Models;
using Xunit;

namespace ShopGold.Aduos.Gaska.ProductsService.Tests
{
    public class ShopGoldCsvWriterTests
    {
        private const string NewLine = "\r\n";

        [Fact]
        public void UpdateFile_ContainsOnlyCatalogNumberAndChangedFields()
        {
            var csv = ShopGoldCsvWriter.Write(new[] { TestData.Row() }, new[] { ShopGoldFields.Stock, ShopGoldFields.Price });
            var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal("Nr_katalogowy;Cena_brutto;Podatek_Vat;Ilosc_produktow", lines[0]);
            Assert.Equal("\"ABC-1\";\"139.99\";\"23\";\"5\"", lines[1]);
            Assert.Equal(2, lines.Length);
        }

        [Fact]
        public void AddFile_HasOneLinePerProduct_AndAllColumns()
        {
            var csv = ShopGoldCsvWriter.Write(new[] { TestData.Row(), TestData.Row(TestData.Product(code: "B")) }, ShopGoldFields.Order);
            var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(3, lines.Length);
            Assert.StartsWith("Nr_katalogowy;Kategoria_1_nazwa;", lines[0]);
            Assert.Contains("Zdjecie_glowne", lines[0]);
            Assert.Contains("GPSR_importer_nazwa", lines[0]);
            Assert.Contains("Opis", lines[0]);

            var columnCount = lines[0].Split(';').Length;
            Assert.All(lines.Skip(1), l => Assert.StartsWith("\"", l));
            Assert.Equal(columnCount, lines[1].Split("\";\"").Length);
        }

        [Fact]
        public void UpdateWithSingleImage_RepeatsMainImageAsFirstExtra_SoShopDropsOldExtras()
        {
            var single = TestData.Row(images: new[] { @"C:\img\image_1.jpg" });

            var update = ShopGoldCsvWriter.Write(new[] { single }, new[] { ShopGoldFields.Images }, isUpdate: true);
            Assert.StartsWith("\"ABC-1\";\"gaska/ABC-1/1_aaaaaaaa.jpg\";\"gaska/ABC-1/1_aaaaaaaa.jpg\";\"\"", update.Split(NewLine)[1]);

            var add = ShopGoldCsvWriter.Write(new[] { single }, new[] { ShopGoldFields.Images });
            Assert.StartsWith("\"ABC-1\";\"gaska/ABC-1/1_aaaaaaaa.jpg\";\"\"", add.Split(NewLine)[1]);

            // Przy kilku zdjęciach nic nie jest dopisywane.
            var multi = ShopGoldCsvWriter.Write(new[] { TestData.Row() }, new[] { ShopGoldFields.Images }, isUpdate: true);
            Assert.StartsWith("\"ABC-1\";\"gaska/ABC-1/1_aaaaaaaa.jpg\";\"gaska/ABC-1/2_aaaaaaaa.png\";\"\"", multi.Split(NewLine)[1]);
        }

        [Fact]
        public void Quote_EscapesQuotesAndRemovesNewLines() =>
            Assert.Equal("\"a \"\"b\"\" c  d\"", ShopGoldCsvWriter.Quote("a \"b\" c\r\nd"));

        [Fact]
        public void Bytes_AreUtf8WithoutBom()
        {
            var bytes = ShopGoldCsvWriter.ToBytes("Ł");
            Assert.Equal(new byte[] { 0xC5, 0x81 }, bytes);
        }
    }
}
