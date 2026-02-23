namespace AduosSyncServices.Infrastructure.Helpers
{
    public static class ImageHelper
    {
        public static async Task<string?> SaveImageAsync(HttpClient httpClient, string url, int productId, string baseDirectory, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url) || productId <= 0)
                return null;

            var productFolder = Path.Combine(baseDirectory, productId.ToString());
            Directory.CreateDirectory(productFolder);

            var imgBytes = await httpClient.GetByteArrayAsync(url, ct);

            var extension = Path.GetExtension(url);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".jpg";

            var counter = 1;
            string filePath;

            do
            {
                var fileName = $"image_{counter}{extension}";
                filePath = Path.Combine(productFolder, fileName);
                counter++;
            }
            while (File.Exists(filePath));

            await File.WriteAllBytesAsync(filePath, imgBytes, ct);
            return filePath;
        }

        public static List<string> GetImageFiles(string folderPath, int productId)
        {
            if (!Directory.Exists(folderPath) || productId <= 0)
                return new List<string>();

            var productFolder = Path.Combine(folderPath, productId.ToString());
            if (!Directory.Exists(productFolder))
                return new List<string>();

            return Directory.EnumerateFiles(productFolder)
                .Where(f =>
                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
