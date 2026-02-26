namespace AduosSyncServices.Infrastructure.Helpers
{
    public static class ImageHelper
    {
        public static async Task<List<string>> SaveImagesAsync(
            HttpClient httpClient,
            List<string> urls,
            int productId,
            string baseDirectory,
            CancellationToken ct = default)
        {
            var savedFiles = new List<string>();

            if (urls == null || urls.Count == 0 || productId <= 0)
                return savedFiles;

            var productFolder = Path.Combine(baseDirectory, productId.ToString());

            // Ensure directory exists
            Directory.CreateDirectory(productFolder);

            // DELETE existing images first
            var existingFiles = Directory.GetFiles(productFolder, "*.*", SearchOption.TopDirectoryOnly);
            foreach (var file in existingFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {

                }
            }

            int counter = 1;

            foreach (var url in urls)
            {
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uriResult) || (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                    continue;

                try
                {
                    var imgBytes = await httpClient.GetByteArrayAsync(url, ct);

                    var extension = Path.GetExtension(url);

                    if (string.IsNullOrWhiteSpace(extension) || extension.Length > 5)
                        extension = ".jpg";

                    var fileName = $"image_{counter}{extension}";
                    var filePath = Path.Combine(productFolder, fileName);

                    await File.WriteAllBytesAsync(filePath, imgBytes, ct);

                    savedFiles.Add(filePath);

                    counter++;
                }
                catch
                {

                }
            }

            return savedFiles;
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
