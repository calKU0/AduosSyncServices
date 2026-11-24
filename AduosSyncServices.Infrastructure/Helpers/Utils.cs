using SkiaSharp;
using System.Globalization;

namespace AduosSyncServices.Infrastructure.Helpers
{
    public static class Utils
    {
        /// <summary>Jakość JPEG po powiększeniu zdjęcia.</summary>
        private const int JpegQuality = 90;

        /// <summary>
        /// Allegro odrzuca zdjęcia mniejsze niż 400×400. Mniejsze powiększamy proporcjonalnie i zapisujemy
        /// jako JPEG (przezroczystość PNG na białym tle). Wystarczająco duże zwracamy bez zmian.
        /// Null = plik nie jest poprawnym obrazem.
        /// </summary>
        public static byte[]? EnsureImageMinSize(byte[] image, int minWidth = 400, int minHeight = 400)
        {
            try
            {
                using var source = SKBitmap.Decode(image);
                if (source == null || source.Width <= 0 || source.Height <= 0)
                    return null;

                if (source.Width >= minWidth && source.Height >= minHeight)
                    return image;

                var scale = Math.Max((double)minWidth / source.Width, (double)minHeight / source.Height);
                var newWidth = (int)Math.Ceiling(source.Width * scale);
                var newHeight = (int)Math.Ceiling(source.Height * scale);

                using var sourceImage = SKImage.FromBitmap(source);
                using var surface = SKSurface.Create(new SKImageInfo(newWidth, newHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
                surface.Canvas.Clear(SKColors.White);
                surface.Canvas.DrawImage(sourceImage, new SKRect(0, 0, newWidth, newHeight), new SKSamplingOptions(SKCubicResampler.Mitchell), null);

                using var result = surface.Snapshot();
                using var data = result.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);

                return data?.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Typ zawartości zdjęcia do wysłania: oryginał według rozszerzenia pliku, obraz powiększony przez
        /// <see cref="EnsureImageMinSize"/> - zawsze JPEG.
        /// </summary>
        public static string GetContentType(string path, byte[] original, byte[] validated) =>
            ReferenceEquals(original, validated) ? GetContentTypeFromPath(path) : "image/jpeg";

        public static string GetContentTypeFromPath(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };
        }

        public static bool TryParseDecimal(string input, out decimal result)
        {
            return decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ||
            decimal.TryParse(input, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
        }
    }
}
