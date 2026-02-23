using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System.Globalization;

namespace AduosSyncServices.Infrastructure.Helpers
{
    public static class Utils
    {
        public static byte[] EnsureImageMinSize(byte[] image, int minWidth = 400, int minHeight = 400)
        {
            try
            {
                using var imageTemp = Image.Load(image);

                if (imageTemp.Width >= minWidth && imageTemp.Height >= minHeight)
                    return image; // already large enough

                // Calculate scale factor to meet minimum size
                double scaleX = (double)minWidth / imageTemp.Width;
                double scaleY = (double)minHeight / imageTemp.Height;
                double scale = Math.Max(scaleX, scaleY);

                int newWidth = (int)(imageTemp.Width * scale);
                int newHeight = (int)(imageTemp.Height * scale);

                imageTemp.Mutate(x => x.Resize(newWidth, newHeight));

                using var ms = new MemoryStream();
                imageTemp.Save(ms, new JpegEncoder());

                return ms.ToArray();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

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
