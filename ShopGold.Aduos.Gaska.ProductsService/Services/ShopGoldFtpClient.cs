using System.Collections.Concurrent;
using FluentFTP;
using Microsoft.Extensions.Options;
using ShopGold.Aduos.Gaska.ProductsService.Helpers;
using ShopGold.Aduos.Gaska.ProductsService.Settings;

namespace ShopGold.Aduos.Gaska.ProductsService.Services
{
    public interface IShopGoldFtpClient : IAsyncDisposable
    {
        /// <summary>Wgrywa plik CSV do katalogu importu sklepu.</summary>
        Task UploadCsvAsync(string fileName, byte[] content, CancellationToken ct);

        Task DeleteCsvAsync(string fileName, CancellationToken ct);

        /// <summary>Ile wywołań <see cref="UploadProductImagesAsync"/> może iść równolegle.</summary>
        int ImageUploadParallelism { get; }

        /// <summary>Wgrywa zdjęcia produktu (ścieżki względne katalogu zdjęć Gąski). Można wywoływać równolegle.</summary>
        Task UploadProductImagesAsync(IReadOnlyList<(string LocalPath, string RemoteFileName)> images, CancellationToken ct);

        /// <summary>
        /// Usuwa z folderu produktu pliki spoza aktualnego zestawu zdjęć. Wywoływane dopiero po udanym
        /// imporcie - wcześniej sklep nadal wskazuje stare pliki.
        /// </summary>
        Task DeleteStaleProductImagesAsync(IReadOnlyList<(string LocalPath, string RemoteFileName)> images, CancellationToken ct);

        /// <summary>
        /// Usuwa z katalogu importu pliki CSV serwisu (gaska_*.csv) starsze niż podana liczba dni - zostają
        /// po nieudanych importach. Pliki wgrane ręcznie (inna nazwa) nie są ruszane. Zwraca liczbę usuniętych.
        /// </summary>
        Task<int> DeleteOldCsvFilesAsync(int olderThanDays, CancellationToken ct);
    }

    public sealed class ShopGoldFtpClient : IShopGoldFtpClient
    {
        private readonly FtpSettings _settings;
        private readonly ILogger<ShopGoldFtpClient> _logger;
        private AsyncFtpClient? _client;

        /// <summary>Osobne połączenia do równoległego wgrywania zdjęć (główne służy do CSV i porządków).</summary>
        private readonly ConcurrentBag<AsyncFtpClient> _imageClients = new();
        private readonly SemaphoreSlim _imageSlots;

        public ShopGoldFtpClient(IOptions<FtpSettings> settings, ILogger<ShopGoldFtpClient> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _imageSlots = new SemaphoreSlim(ImageUploadParallelism);
        }

        /// <summary>Ile zdjęć produktów można wgrywać naraz.</summary>
        public int ImageUploadParallelism => Math.Clamp(_settings.ImageUploadConnections, 1, 10);

        public async Task UploadCsvAsync(string fileName, byte[] content, CancellationToken ct)
        {
            var client = await GetClientAsync(ct);
            var remotePath = ShopGoldRowFactory.JoinPath(_settings.ImportDirectory, fileName);

            var status = await client.UploadBytes(content, remotePath, FtpRemoteExists.Overwrite, createRemoteDir: true, token: ct);
            if (status == FtpStatus.Failed)
                throw new IOException($"FTP upload of {remotePath} failed.");
        }

        public async Task DeleteCsvAsync(string fileName, CancellationToken ct)
        {
            var client = await GetClientAsync(ct);
            var remotePath = ShopGoldRowFactory.JoinPath(_settings.ImportDirectory, fileName);

            if (await client.FileExists(remotePath, ct))
                await client.DeleteFile(remotePath, ct);
        }

        /// <summary>Przedrostek nazw plików CSV tworzonych przez serwis.</summary>
        public const string CsvFilePrefix = "gaska_";

        public async Task<int> DeleteOldCsvFilesAsync(int olderThanDays, CancellationToken ct)
        {
            if (olderThanDays <= 0)
                return 0;

            var client = await GetClientAsync(ct);
            var cutoff = DateTime.Now.AddDays(-olderThanDays);
            var deleted = 0;

            foreach (var item in await client.GetListing(_settings.ImportDirectory, ct))
            {
                if (!IsOldServiceCsv(item.Type == FtpObjectType.File, item.Name, item.Modified, cutoff))
                    continue;

                await client.DeleteFile(item.FullName, ct);
                deleted++;
            }

            return deleted;
        }

        /// <remarks>
        /// Plik bez daty modyfikacji (serwer jej nie podał) zostawiamy - lepiej nie usunąć niż usunąć świeży.
        /// </remarks>
        internal static bool IsOldServiceCsv(bool isFile, string name, DateTime modified, DateTime cutoff) =>
            isFile
            && name.StartsWith(CsvFilePrefix, StringComparison.OrdinalIgnoreCase)
            && name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            && modified != DateTime.MinValue
            && modified < cutoff;

        /// <remarks>
        /// Bezpieczne do wywoływania równolegle: każde wywołanie bierze własne połączenie z puli
        /// (najwyżej <see cref="FtpSettings.ImageUploadConnections"/>). Folder produktu zakładamy raz,
        /// pliki wysyłamy bez sprawdzania, czy istnieją - i tak je nadpisujemy, a każde sprawdzenie to
        /// osobne zapytanie do serwera.
        /// </remarks>
        public async Task UploadProductImagesAsync(IReadOnlyList<(string LocalPath, string RemoteFileName)> images, CancellationToken ct)
        {
            if (images.Count == 0)
                return;

            await _imageSlots.WaitAsync(ct);
            AsyncFtpClient? client = null;
            var healthy = false;
            try
            {
                client = _imageClients.TryTake(out var pooled) && pooled.IsConnected ? pooled : await ConnectAsync(ct);

                foreach (var folder in images.Select(i => ImageFolder(i.RemoteFileName)).Distinct(StringComparer.Ordinal))
                    await client.CreateDirectory(folder, true, ct);

                foreach (var (localPath, remoteFileName) in images)
                {
                    var remotePath = ShopGoldRowFactory.JoinPath(_settings.ImagesDirectory, remoteFileName);
                    var status = await client.UploadFile(localPath, remotePath, FtpRemoteExists.NoCheck, createRemoteDir: false, token: ct);

                    if (status == FtpStatus.Failed)
                        throw new IOException($"FTP upload of {remotePath} failed.");
                }

                healthy = true;
            }
            finally
            {
                // Połączenie po błędzie zamykamy - następne wywołanie otworzy nowe.
                if (client != null)
                {
                    if (healthy)
                        _imageClients.Add(client);
                    else
                        await CloseAsync(client);
                }

                _imageSlots.Release();
            }
        }

        private string ImageFolder(string remoteFileName)
        {
            var folder = Path.GetDirectoryName(remoteFileName.Replace('\\', '/'))?.Replace('\\', '/') ?? string.Empty;
            return ShopGoldRowFactory.JoinPath(_settings.ImagesDirectory, folder);
        }

        public async Task DeleteStaleProductImagesAsync(IReadOnlyList<(string LocalPath, string RemoteFileName)> images, CancellationToken ct)
        {
            if (images.Count == 0)
                return;

            var client = await GetClientAsync(ct);

            // Stare wersje zdjęć (inny skrót w nazwie) nie są już nigdzie używane.
            var folder = ShopGoldRowFactory.JoinPath(_settings.ImagesDirectory, Path.GetDirectoryName(images[0].RemoteFileName)!.Replace('\\', '/'));
            var keep = images
                .Select(i => Path.GetFileName(i.RemoteFileName))
                .ToList();

            try
            {
                foreach (var item in await client.GetListing(folder, ct))
                {
                    if (item.Type == FtpObjectType.File && !keep.Contains(item.Name, StringComparer.OrdinalIgnoreCase))
                        await client.DeleteFile(item.FullName, ct);

                    // Sklep trzyma miniatury w podfolderze (mini/100px_1_abc.webp) - stare też usuwamy.
                    if (item.Type == FtpObjectType.Directory)
                    {
                        foreach (var thumb in await client.GetListing(item.FullName, ct))
                        {
                            if (thumb.Type == FtpObjectType.File && !IsThumbnailOf(thumb.Name, keep))
                                await client.DeleteFile(thumb.FullName, ct);
                        }
                    }
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Removing old images from {Folder} failed.", folder);
            }
        }

        /// <summary>
        /// Czy plik miniatury sklepu należy do któregoś z aktualnych zdjęć: "450px_1_abc.webp" to miniatura
        /// zdjęcia "1_abc.jpg" (rozmiar z przodu, rozszerzenie dowolne).
        /// </summary>
        internal static bool IsThumbnailOf(string thumbnailName, IEnumerable<string> imageNames)
        {
            var thumbBase = Path.GetFileNameWithoutExtension(thumbnailName);

            return imageNames.Any(image =>
            {
                var imageBase = Path.GetFileNameWithoutExtension(image);
                return thumbBase.Equals(imageBase, StringComparison.OrdinalIgnoreCase)
                    || thumbBase.EndsWith("_" + imageBase, StringComparison.OrdinalIgnoreCase);
            });
        }

        private async Task<AsyncFtpClient> GetClientAsync(CancellationToken ct)
        {
            if (_client is { IsConnected: true })
                return _client;

            if (_client != null)
                await _client.DisposeAsync();

            _client = await ConnectAsync(ct);
            return _client;
        }

        private async Task<AsyncFtpClient> ConnectAsync(CancellationToken ct)
        {
            var client = new AsyncFtpClient(_settings.Host, _settings.User, _settings.Password, _settings.Port);
            client.Config.EncryptionMode = _settings.UseFtps ? FtpEncryptionMode.Explicit : FtpEncryptionMode.None;
            client.Config.ValidateAnyCertificate = true;
            client.Config.RetryAttempts = 3;
            client.Config.ConnectTimeout = 30000;
            client.Config.ReadTimeout = 60000;
            client.Config.DataConnectionConnectTimeout = 30000;
            client.Config.DataConnectionReadTimeout = 60000;

            try
            {
                await client.Connect(ct);
                return client;
            }
            catch
            {
                await client.DisposeAsync();
                throw;
            }
        }

        private static async Task CloseAsync(AsyncFtpClient client)
        {
            try
            {
                if (client.IsConnected)
                    await client.Disconnect();
            }
            catch
            {
                // Zamykanie połączenia nie może przerwać cyklu.
            }

            await client.DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            while (_imageClients.TryTake(out var imageClient))
                await CloseAsync(imageClient);

            if (_client != null)
            {
                await CloseAsync(_client);
                _client = null;
            }

            _imageSlots.Dispose();
        }
    }
}
