using AduosSyncServices.Infrastructure.Helpers;
using Xunit;

namespace AduosSyncServices.Infrastructure.Tests.Helpers
{
    public class ImageHelperTests : IDisposable
    {
        private readonly string _root;

        public ImageHelperTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "ImageHelperTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        private string CreateProductFile(int productId, string fileName)
        {
            var folder = Path.Combine(_root, productId.ToString());
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, fileName);
            File.WriteAllBytes(path, new byte[] { 0x1 });
            return path;
        }

        [Fact]
        public void GetImageFiles_ReturnsOnlyImageExtensions_SortedByName()
        {
            CreateProductFile(5, "b.png");
            CreateProductFile(5, "a.jpg");
            CreateProductFile(5, "notes.txt");

            var files = ImageHelper.GetImageFiles(_root, 5);

            Assert.Equal(2, files.Count);
            Assert.EndsWith("a.jpg", files[0]);
            Assert.EndsWith("b.png", files[1]);
        }

        [Fact]
        public void GetFirstImageFile_ReturnsAlphabeticallyFirstImage()
        {
            CreateProductFile(7, "z.jpg");
            CreateProductFile(7, "a.jpeg");

            var first = ImageHelper.GetFirstImageFile(_root, 7);

            Assert.NotNull(first);
            Assert.EndsWith("a.jpeg", first);
        }

        [Fact]
        public void GetFirstImageFile_MissingProductFolder_ReturnsNull()
            => Assert.Null(ImageHelper.GetFirstImageFile(_root, 999));

        [Fact]
        public void GetImageFiles_InvalidProductId_ReturnsEmpty()
            => Assert.Empty(ImageHelper.GetImageFiles(_root, 0));

        [Fact]
        public void GetImageFiles_MissingRootFolder_ReturnsEmpty()
            => Assert.Empty(ImageHelper.GetImageFiles(Path.Combine(_root, "does-not-exist"), 5));
    }
}
