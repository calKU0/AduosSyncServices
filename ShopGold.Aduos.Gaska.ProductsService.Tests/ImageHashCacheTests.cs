using ShopGold.Aduos.Gaska.ProductsService.Helpers;
using Xunit;

namespace ShopGold.Aduos.Gaska.ProductsService.Tests
{
    public sealed class ImageHashCacheTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "imghash_" + Guid.NewGuid().ToString("N"));
        private string CacheFile => Path.Combine(_dir, "cache", "image-hashes.tsv");

        public ImageHashCacheTests() => Directory.CreateDirectory(_dir);

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        private string Image(string name, string content)
        {
            var path = Path.Combine(_dir, name);
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>Podmienia zawartość pliku, zostawiając rozmiar i datę zapisu - jak plik, którego nie trzeba czytać.</summary>
        private static void ReplaceKeepingMetadata(string path, string content)
        {
            var time = File.GetLastWriteTimeUtc(path);
            File.WriteAllText(path, content);
            File.SetLastWriteTimeUtc(path, time);
        }

        [Fact]
        public void ComputeHash_ReturnsNull_ForNoImages()
        {
            Assert.Null(new ImageHashCache().ComputeHash(Array.Empty<string>()));
        }

        [Fact]
        public void Hashes_SurviveRestart_WithoutReadingFilesAgain()
        {
            var image = Image("1.jpg", "aaaa");
            var first = new ImageHashCache(CacheFile);
            var hash = first.ComputeHash(new[] { image });
            first.Save();

            ReplaceKeepingMetadata(image, "bbbb");
            var afterRestart = new ImageHashCache(CacheFile).ComputeHash(new[] { image });

            Assert.Equal(hash, afterRestart);
        }

        [Fact]
        public void ChangedFile_IsHashedAgain()
        {
            var image = Image("1.jpg", "aaaa");
            var cache = new ImageHashCache(CacheFile);
            var hash = cache.ComputeHash(new[] { image });
            cache.Save();

            File.WriteAllText(image, "bbbbbb");

            Assert.NotEqual(hash, new ImageHashCache(CacheFile).ComputeHash(new[] { image }));
        }

        [Fact]
        public void Save_RemovesUnusedFiles_OnlyWhenAsked()
        {
            var a = Image("a.jpg", "aaaa");
            var b = Image("b.jpg", "bbbb");
            var cache = new ImageHashCache(CacheFile);
            cache.ComputeHash(new[] { a, b });
            cache.Save();

            cache.ComputeHash(new[] { a });
            cache.Save(removeUnused: false);
            Assert.Equal(2, File.ReadAllLines(CacheFile).Length);

            cache.Save();
            Assert.Single(File.ReadAllLines(CacheFile));
        }

        [Fact]
        public void CorruptCacheFile_IsIgnored()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
            File.WriteAllText(CacheFile, "garbage\nmore\tgarbage");
            var image = Image("1.jpg", "aaaa");

            var hash = new ImageHashCache(CacheFile).ComputeHash(new[] { image });

            Assert.Equal(new ImageHashCache().ComputeHash(new[] { image }), hash);
        }
    }
}
