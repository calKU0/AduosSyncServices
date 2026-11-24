using AduosSyncServices.Infrastructure.Helpers;
using SkiaSharp;
using Xunit;

namespace AduosSyncServices.Infrastructure.Tests.Helpers
{
    public class UtilsImageTests
    {
        private static byte[] CreateImage(int width, int height, SKEncodedImageFormat format, SKColor color)
        {
            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            surface.Canvas.Clear(color);
            using var image = surface.Snapshot();
            using var data = image.Encode(format, 100);
            return data.ToArray();
        }

        [Fact]
        public void LargeEnoughImage_IsReturnedUnchanged()
        {
            var original = CreateImage(800, 600, SKEncodedImageFormat.Png, SKColors.Red);

            var result = Utils.EnsureImageMinSize(original);

            Assert.Same(original, result);
            Assert.Equal("image/png", Utils.GetContentType("a.png", original, result!));
        }

        [Fact]
        public void SmallImage_IsScaledProportionally_ToAtLeastMinimum_AsJpeg()
        {
            var original = CreateImage(200, 100, SKEncodedImageFormat.Png, SKColors.Blue);

            var result = Utils.EnsureImageMinSize(original);

            Assert.NotNull(result);
            using var decoded = SKBitmap.Decode(result);
            Assert.Equal(800, decoded.Width);
            Assert.Equal(400, decoded.Height);

            using var codec = SKCodec.Create(new MemoryStream(result!));
            Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
            Assert.Equal("image/jpeg", Utils.GetContentType("a.png", original, result!));
        }

        [Fact]
        public void TransparentPng_GetsWhiteBackground()
        {
            var original = CreateImage(100, 100, SKEncodedImageFormat.Png, SKColors.Transparent);

            var result = Utils.EnsureImageMinSize(original);

            using var decoded = SKBitmap.Decode(result);
            var pixel = decoded.GetPixel(200, 200);
            Assert.True(pixel.Red > 245 && pixel.Green > 245 && pixel.Blue > 245, $"Expected white, got {pixel}");
        }

        [Fact]
        public void InvalidData_ReturnsNull() =>
            Assert.Null(Utils.EnsureImageMinSize(new byte[] { 1, 2, 3, 4 }));
    }
}
