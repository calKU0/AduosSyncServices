using ShopGold.Aduos.Gaska.ProductsService.Services;
using Xunit;

namespace ShopGold.Aduos.Gaska.ProductsService.Tests
{
    public class ShopGoldFtpClientTests
    {
        private static readonly DateTime Cutoff = new(2026, 11, 16);

        [Theory]
        [InlineData(true, "gaska_aktualizacja_20261101_101010_123.csv", 2026, 11, 1, true)]
        [InlineData(true, "gaska_dodawanie_20261120_101010_123.csv", 2026, 11, 20, false)]   // świeży
        [InlineData(true, "export_csv_23_11_2026_08_37_42.csv", 2026, 11, 1, false)]        // plik wgrany ręcznie
        [InlineData(true, "shopgold_wysylka_id1.csv", 2026, 11, 1, false)]
        [InlineData(true, "gaska_notatki.txt", 2026, 11, 1, false)]
        [InlineData(false, "gaska_folder.csv", 2026, 11, 1, false)]
        public void OnlyOldServiceCsvFiles_AreDeleted(bool isFile, string name, int y, int m, int d, bool expected) =>
            Assert.Equal(expected, ShopGoldFtpClient.IsOldServiceCsv(isFile, name, new DateTime(y, m, d), Cutoff));

        [Theory]
        [InlineData("100px_1_7be775f1.webp", true)]
        [InlineData("450px_2_7be775f1.webp", true)]
        [InlineData("1_7be775f1.webp", true)]
        [InlineData("450px_1_aaaaaaaa.webp", false)]   // miniatura starej wersji zdjęć
        [InlineData("450px_3_7be775f1.webp", false)]   // zdjęcia nr 3 już nie ma
        public void Thumbnails_AreMatchedToCurrentImages(string thumbnail, bool expected) =>
            Assert.Equal(expected, ShopGoldFtpClient.IsThumbnailOf(thumbnail, new[] { "1_7be775f1.jpg", "2_7be775f1.jpg" }));

        [Fact]
        public void FileWithoutModificationDate_IsKept() =>
            Assert.False(ShopGoldFtpClient.IsOldServiceCsv(true, "gaska_x.csv", DateTime.MinValue, Cutoff));
    }
}
