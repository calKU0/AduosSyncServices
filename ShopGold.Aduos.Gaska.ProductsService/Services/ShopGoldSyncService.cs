using System.Collections.Concurrent;
using AduosSyncServices.Contracts.Data;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.Infrastructure.Helpers;
using Microsoft.Extensions.Options;
using ShopGold.Aduos.Gaska.ProductsService.Constants;
using ShopGold.Aduos.Gaska.ProductsService.Helpers;
using ShopGold.Aduos.Gaska.ProductsService.Models;
using ShopGold.Aduos.Gaska.ProductsService.Repositories;
using ShopGold.Aduos.Gaska.ProductsService.Settings;

namespace ShopGold.Aduos.Gaska.ProductsService.Services
{
    public interface IShopGoldSyncService
    {
        Task SyncAsync(CancellationToken ct);
    }

    /// <summary>
    /// Gąska -> ShopGold. Produktów nie pobiera z Gąski - korzysta z bazy, którą wypełnia serwis Allegro
    /// (w tym zdjęcia zapisane lokalnie). Buduje pliki CSV, wgrywa je i zdjęcia na FTP sklepu
    /// i uruchamia import w panelu. Do sklepu idą tylko zmiany od ostatniego udanego importu.
    /// </summary>
    public class ShopGoldSyncService : IShopGoldSyncService
    {
        /// <summary>Najwięcej wierszy w jednym pliku CSV - dłuższy import łatwiej przerwać (sesja, limity serwera).</summary>
        private const int MaxRowsPerFile = 3000;

        /// <summary>Co ile produktów logować postęp przygotowania wierszy.</summary>
        private const int ProgressLogInterval = 5000;

        /// <summary>Co ile produktów logować postęp wgrywania zdjęć.</summary>
        private const int ImageProgressLogInterval = 250;

        private readonly IShopGoldRepository _repository;
        private readonly ISyncCategoryRepository _syncCategoryRepository;
        private readonly IShopGoldFtpClient _ftp;
        private readonly IShopGoldPanelClient _panel;
        private readonly ImageHashCache _imageHashes;
        private readonly ShopCategoryCache _categoryCache;
        private readonly AppSettings _appSettings;
        private readonly ShopGoldSettings _shopSettings;
        private readonly FtpSettings _ftpSettings;
        private readonly GpsrSettings _gpsrSettings;
        private readonly PriceSettings _priceSettings;
        private readonly ILogger<ShopGoldSyncService> _logger;

        /// <summary>Czy w tym cyklu udał się choć jeden import (wtedy czyścimy cache sklepu).</summary>
        private bool _importedAnything;

        public ShopGoldSyncService(
            IShopGoldRepository repository,
            ISyncCategoryRepository syncCategoryRepository,
            IShopGoldFtpClient ftp,
            IShopGoldPanelClient panel,
            ImageHashCache imageHashes,
            ShopCategoryCache categoryCache,
            IOptions<AppSettings> appSettings,
            IOptions<ShopGoldSettings> shopSettings,
            IOptions<FtpSettings> ftpSettings,
            IOptions<GpsrSettings> gpsrSettings,
            IOptions<PriceSettings> priceSettings,
            ILogger<ShopGoldSyncService> logger)
        {
            _repository = repository;
            _syncCategoryRepository = syncCategoryRepository;
            _ftp = ftp;
            _panel = panel;
            _imageHashes = imageHashes;
            _categoryCache = categoryCache;
            _appSettings = appSettings.Value;
            _shopSettings = shopSettings.Value;
            _ftpSettings = ftpSettings.Value;
            _gpsrSettings = gpsrSettings.Value;
            _priceSettings = priceSettings.Value;
            _logger = logger;
        }

        public async Task SyncAsync(CancellationToken ct)
        {
            _importedAnything = false;
            var categories = _appSettings.GetCategoryIds();

            // Kategorie ShopGold zapisujemy zawsze - z nich serwis Allegro wie, co jeszcze pobrać z Gąski.
            await _syncCategoryRepository.ReplaceChannelCategoriesAsync(SyncChannels.ShopGold, categories, ct);

            if (categories.Count == 0)
            {
                _logger.LogWarning("No Gąska categories configured for ShopGold (AppSettings:CategoriesId). Nothing to send.");
                return;
            }

            if (_priceSettings.MarginRanges.Count == 0)
            {
                _logger.LogError("No margin ranges configured (PriceSettings:MarginRanges). Prices cannot be calculated - sync skipped.");
                return;
            }

            var excludedPaths = await ResolveExcludedCategoryPathsAsync(ct);
            if (excludedPaths == null)
                return;

            var products = await _repository.GetProductsAsync(ct);
            var states = (await _repository.GetStatesAsync(ct))
                .GroupBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("ShopGold: {Products} products in configured categories, {States} products already sent to the shop.", products.Count, states.Count);

            var shopCategories = await GetShopCategoriesAsync(ct);
            var rows = BuildRows(products, shopCategories, excludedPaths, ct);

            if (shopCategories != null)
            {
                // Brakujące kategorie zakładamy importem z nazwami kategorii (sklep tworzy je sam), potem
                // czytamy drzewo jeszcze raz - dopiero wtedy znamy id do Kategorie_id.
                var representatives = await CreateMissingCategoriesAsync(rows, states, ct);
                if (representatives.Count > 0)
                {
                    shopCategories = await GetShopCategoriesAsync(ct);
                    rows = BuildRows(products, shopCategories, excludedPaths, ct);

                    // Import zakładający kategorie przypisał te produkty do jednej kategorii - Kategorie_id
                    // trzeba im wysłać ponownie, nawet jeśli lista się nie zmieniła.
                    foreach (var code in representatives)
                    {
                        if (states.TryGetValue(code, out var state))
                            states[code] = ShopGoldSyncPlanner.WithoutField(state, ShopGoldFields.CategoryIds);
                    }
                }

                var stillMissing = rows.SelectMany(r => r.MissingCategoryPaths).Select(ShopCategoryMap.Key).Distinct().ToList();
                if (stillMissing.Count > 0)
                    _logger.LogInformation(
                        "{Count} Gąska categories are not in the shop yet (products keep their main category until then), e.g.: {Examples}",
                        stillMissing.Count, string.Join(" | ", stillMissing.Take(5)));
            }

            // Pusta lista produktów (np. zaraz po wdrożeniu, zanim serwis Allegro zapisze powiązania kategorii)
            // nie może wyłączyć w sklepie wszystkiego, co już tam jest.
            var allowDeactivation = rows.Count > 0;
            if (!allowDeactivation && states.Values.Any(s => s.IsActive))
                _logger.LogWarning("No products available for ShopGold - deactivating products in the shop was skipped this cycle.");

            var plan = ShopGoldSyncPlanner.Plan(rows, states, _appSettings.MaxNewProductsPerCycle, _appSettings.SkipProductsWithoutImages, allowDeactivation);

            _logger.LogInformation(
                "ShopGold plan: {Adds} to add, {Updates} to update in {Files} files ({Deactivations} to deactivate), {Unchanged} unchanged, " +
                "skipped new: {NoImages} without images, {Inactive} unavailable, {Deferred} deferred to next cycle.",
                plan.Adds.Count, plan.Updates.Sum(u => u.Rows.Count), plan.Updates.Count, plan.Deactivations, plan.Unchanged,
                plan.SkippedWithoutImages, plan.SkippedInactiveNew, plan.DeferredNew);

            try
            {
                foreach (var chunk in plan.Adds.Chunk(MaxRowsPerFile))
                    await ImportBatchAsync(new ShopGoldImportBatch(ShopGoldFields.AddFields, chunk.ToList()), ShopGoldImportMode.Add, states, ct);

                foreach (var batch in plan.Updates)
                {
                    foreach (var chunk in batch.Rows.Chunk(MaxRowsPerFile))
                        await ImportBatchAsync(new ShopGoldImportBatch(batch.Fields, chunk.ToList()), ShopGoldImportMode.Update, states, ct);
                }

                if (_importedAnything)
                    await ClearShopCacheAsync(ct);

                await DeleteOldCsvFilesAsync(ct);
            }
            finally
            {
                await _ftp.DisposeAsync();
            }
        }

        /// <summary>
        /// Ścieżki pomijanych kategorii Gąski (z drzewa kategorii, które odświeża serwis Allegro). Null = nie wszystkie
        /// id są znane: cykl jest wstrzymywany, bo sklep dostałby kategorie, które miały być pominięte (np. Wyprzedaż).
        /// </summary>
        private async Task<List<IReadOnlyList<string>>?> ResolveExcludedCategoryPathsAsync(CancellationToken ct)
        {
            var ids = _appSettings.ExcludedCategoryIds.Where(id => id != 0).Distinct().ToList();
            var paths = await _repository.GetGaskaCategoryPathsAsync(ids, ct);

            var unknown = ids.Where(id => !paths.ContainsKey(id)).ToList();
            if (unknown.Count > 0)
            {
                _logger.LogError(
                    "Excluded Gąska categories {Ids} are not in the Gąska category tree (GaskaCategories) - the Allegro products service " +
                    "fills it on its next product sync. ShopGold sync skipped so that excluded categories are not created in the shop.",
                    string.Join(", ", unknown));
                return null;
            }

            return paths.Values.Select(ShopGoldRowFactory.ParseCategoryPath).Where(p => p.Count > 0).ToList();
        }

        private List<ShopGoldRow> BuildRows(
            IReadOnlyCollection<AduosSyncServices.Contracts.Models.Product> products,
            ShopCategoryMap? shopCategories,
            IReadOnlyCollection<IReadOnlyList<string>> excludedPaths,
            CancellationToken ct)
        {
            var rows = new List<ShopGoldRow>(products.Count);
            var done = 0;
            var onlyExcluded = 0;
            var completed = false;
            try
            {
                foreach (var product in products)
                {
                    ct.ThrowIfCancellationRequested();

                    // Pierwszy cykl po wdrożeniu czyta zdjęcia wszystkich produktów - długo, więc pokazujemy postęp
                    // i co jakiś czas zapisujemy skróty, żeby restart w trakcie nie zaczynał od zera.
                    if (++done % ProgressLogInterval == 0)
                    {
                        _logger.LogInformation("ShopGold: prepared {Done}/{Total} products (images and CSV rows).", done, products.Count);
                        SaveImageHashes(removeUnused: false);
                    }

                    // Produkt tylko z pomijanych kategorii nie jest integrowany (brak wiersza = wyłączenie, jeśli był w sklepie).
                    if (!ShopGoldRowFactory.HasAllowedCategory(product, excludedPaths))
                    {
                        onlyExcluded++;
                        continue;
                    }

                    try
                    {
                        var images = ImageHelper.GetImageFiles(ServiceConstants.ImagesFolder, product.Id);
                        var imagesHash = _imageHashes.ComputeHash(images);
                        rows.Add(ShopGoldRowFactory.Build(product, images, imagesHash, _appSettings, _shopSettings, _ftpSettings, _gpsrSettings, _priceSettings, shopCategories, excludedPaths));
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        _logger.LogError(ex, "Building ShopGold row for product {Code} failed.", product.Code);
                    }
                }

                completed = true;

                if (onlyExcluded > 0)
                    _logger.LogInformation("ShopGold: {Count} products skipped - they are only in excluded categories.", onlyExcluded);
            }
            finally
            {
                // Zdjęcia produktów spoza listy usuwamy z pliku skrótów tylko po pełnym przejściu.
                SaveImageHashes(removeUnused: completed);
            }

            return rows;
        }

        private void SaveImageHashes(bool removeUnused)
        {
            try
            {
                _imageHashes.Save(removeUnused);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Saving image hashes failed - they will be computed again after a restart.");
            }
        }

        /// <summary>Drzewo kategorii sklepu (z pamięci albo z panelu). Null = niedostępne - wtedy bez Kategorie_id.</summary>
        private async Task<ShopCategoryMap?> GetShopCategoriesAsync(CancellationToken ct)
        {
            var cached = _categoryCache.Current;
            if (cached != null)
                return cached;

            try
            {
                var map = new ShopCategoryMap(await _panel.GetCategoriesAsync(ct));
                _categoryCache.Set(map);
                _logger.LogInformation("ShopGold category tree read from the panel: {Count} categories.", map.Count);
                return map;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Reading ShopGold categories failed - products are sent without additional categories this cycle.");
                return null;
            }
        }

        /// <summary>
        /// Zakłada w sklepie brakujące kategorie Gąski: import aktualizacji z samymi nazwami kategorii
        /// (sklep tworzy kategorię, której nie ma) - po jednym wierszu na ścieżkę, z produktem, który
        /// już jest w sklepie. Nowe produkty dostają kategorię główną przy dodawaniu, a pozostałe
        /// w następnym cyklu. Zwraca numery katalogowe użytych produktów.
        /// </summary>
        private async Task<HashSet<string>> CreateMissingCategoriesAsync(
            IReadOnlyList<ShopGoldRow> rows,
            IReadOnlyDictionary<string, ShopGoldProductState> states,
            CancellationToken ct)
        {
            var creationRows = new List<ShopGoldRow>();
            var pathKeys = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in rows.Where(r => states.ContainsKey(r.CatalogNumber)))
            {
                foreach (var path in row.MissingCategoryPaths)
                {
                    var key = ShopCategoryMap.Key(path);
                    if (_categoryCache.WasCreationAttempted(key) || !seen.Add(key))
                        continue;

                    var creation = new ShopGoldRow
                    {
                        CatalogNumber = row.CatalogNumber,
                        ProductId = row.ProductId,
                        ImagesHash = row.ImagesHash,
                        IsActive = row.IsActive
                    };
                    creation.Add(ShopGoldRowFactory.CategoryField(path));

                    creationRows.Add(creation);
                    pathKeys.Add(key);
                }
            }

            if (creationRows.Count == 0)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Creating {Count} missing categories in ShopGold.", creationRows.Count);

            var result = await ImportFileAsync(creationRows, new[] { ShopGoldFields.Category }, ShopGoldImportMode.Update, ct);
            if (result == null)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in pathKeys)
                _categoryCache.MarkCreationAttempted(key);

            _categoryCache.Invalidate();
            return creationRows.Select(r => r.CatalogNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private async Task ImportBatchAsync(
            ShopGoldImportBatch batch,
            ShopGoldImportMode mode,
            IReadOnlyDictionary<string, ShopGoldProductState> states,
            CancellationToken ct)
        {
            var (rows, uploaded) = await UploadChangedImagesAsync(batch, states, ct);
            if (rows.Count == 0)
                return;

            var result = await ImportFileAsync(rows, batch.Fields, mode, ct);
            if (result == null)
                return;

            // Dodawanie po nazwach mogło założyć nowe kategorie - w następnym cyklu czytamy drzewo od nowa.
            if (mode == ShopGoldImportMode.Add)
                _categoryCache.Invalidate();

            var imported = rows;

            if (mode == ShopGoldImportMode.Add)
            {
                // Tryb "dodawanie" pomija numery katalogowe, które już są w sklepie (np. przejmowane
                // produkty dodane wcześniej ręcznie). Te wiersze wysyłamy od razu w trybie aktualizacji
                // - inaczej zapisalibyśmy je jako wysłane, a w sklepie zostałyby stare dane.
                var existing = ShopGoldSyncPlanner.RowsNotAdded(rows, result.Added, result.AddedNames);

                if (existing.Count > 0)
                {
                    _logger.LogInformation("{Count} of {Rows} products were already in the shop (skipped by add import) - updating them.", existing.Count, rows.Count);

                    var updated = await ImportFileAsync(existing, batch.Fields, ShopGoldImportMode.Update, ct);

                    // Bez udanej aktualizacji te produkty nie mają w sklepie naszych danych - zostają
                    // bez stanu i w następnym cyklu pójdą ponownie.
                    if (updated == null)
                        imported = rows.Except(existing).ToList();
                }
            }

            var newStates = imported
                .Select(r => ShopGoldSyncPlanner.StateAfterImport(r, batch.Fields, states.TryGetValue(r.CatalogNumber, out var s) ? s : null))
                .ToList();

            // Produkty bez wcześniej wysłanych zdjęć nie mają w sklepie starych plików - nie listujemy ich folderów.
            await DeleteStaleImagesAsync(
                uploaded.Intersect(imported)
                    .Where(r => states.TryGetValue(r.CatalogNumber, out var s) && s.ImagesHash != null)
                    .ToList(),
                ct);

            try
            {
                await _repository.SaveStatesAsync(newStates, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Bez zapisanego stanu te produkty pójdą w następnym cyklu jeszcze raz - nic się nie zgubi.
                _logger.LogError(ex, "Saving ShopGold state of {Rows} imported products failed.", newStates.Count);
            }
        }

        /// <summary>Stare wersje zdjęć usuwamy dopiero, gdy sklep wskazuje już nowe pliki.</summary>
        private async Task DeleteStaleImagesAsync(IReadOnlyList<ShopGoldRow> rows, CancellationToken ct)
        {
            foreach (var row in rows)
            {
                try
                {
                    await _ftp.DeleteStaleProductImagesAsync(row.Images, ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Removing old images of {Code} from the shop FTP failed.", row.CatalogNumber);
                }
            }
        }

        /// <summary>Pliki CSV po nieudanych importach zostają do wglądu - po kilku dniach je usuwamy.</summary>
        private async Task DeleteOldCsvFilesAsync(CancellationToken ct)
        {
            try
            {
                var deleted = await _ftp.DeleteOldCsvFilesAsync(_ftpSettings.OldCsvRetentionDays, ct);
                if (deleted > 0)
                    _logger.LogInformation("Deleted {Count} old ShopGold CSV files (older than {Days} days) from the import directory.", deleted, _ftpSettings.OldCsvRetentionDays);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Deleting old ShopGold CSV files failed.");
            }
        }

        /// <summary>Instrukcja ShopGold: po imporcie CSV/XML cache trzeba wyczyścić, inaczej sklep pokazuje stare dane.</summary>
        private async Task ClearShopCacheAsync(CancellationToken ct)
        {
            try
            {
                await _panel.ResetCacheAsync(ct);
                _logger.LogInformation("ShopGold cache cleared.");
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Clearing the ShopGold cache failed - the shop may show old data until its cache refreshes.");
            }
        }

        /// <summary>Wgrywa plik CSV i uruchamia import. Null = import się nie udał (stan nie jest zapisywany).</summary>
        private async Task<ShopGoldImportResult?> ImportFileAsync(
            IReadOnlyList<ShopGoldRow> rows,
            IReadOnlyList<string> fields,
            ShopGoldImportMode mode,
            CancellationToken ct)
        {
            var fileName = $"{ShopGoldFtpClient.CsvFilePrefix}{(mode == ShopGoldImportMode.Add ? "dodawanie" : "aktualizacja")}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.csv";
            var csv = ShopGoldCsvWriter.Write(rows, fields, isUpdate: mode == ShopGoldImportMode.Update);

            try
            {
                await _ftp.UploadCsvAsync(fileName, ShopGoldCsvWriter.ToBytes(csv), ct);

                var result = await _panel.ImportCsvAsync(fileName, mode, ct);

                _logger.LogInformation(
                    "ShopGold import {File} ({Mode}, fields: {Fields}): {Rows} rows, processed {Processed}, added {Added}, updated {Updated}.",
                    fileName, mode, string.Join(", ", fields), rows.Count, result.Processed, result.Added, result.Updated);

                if (_shopSettings.DeleteCsvAfterImport)
                    await TryDeleteCsvAsync(fileName, ct);

                _importedAnything = true;
                return result;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Stan nie jest zapisywany - te same zmiany pójdą w następnym cyklu. Plik zostaje na serwerze do wglądu.
                _logger.LogError(ex, "ShopGold import of {File} ({Mode}, {Rows} rows) failed.", fileName, mode, rows.Count);
                return null;
            }
        }

        /// <summary>
        /// Wgrywa zdjęcia wierszy, w których zdjęcia się zmieniły (lub produkt jest nowy). Wiersz, którego
        /// zdjęć nie udało się wgrać, czeka do następnego cyklu - inaczej sklep wskazywałby nieistniejące pliki.
        /// Zwraca wiersze do importu i te z nich, którym wgraliśmy zdjęcia.
        /// </summary>
        private async Task<(List<ShopGoldRow> Rows, List<ShopGoldRow> Uploaded)> UploadChangedImagesAsync(
            ShopGoldImportBatch batch,
            IReadOnlyDictionary<string, ShopGoldProductState> states,
            CancellationToken ct)
        {
            var uploadedRows = new List<ShopGoldRow>();

            if (!batch.Fields.Contains(ShopGoldFields.Images))
                return (batch.Rows, uploadedRows);

            var toUpload = batch.Rows
                .Where(row => row.Images.Count > 0
                    && !string.Equals(states.TryGetValue(row.CatalogNumber, out var state) ? state.ImagesHash : null, row.ImagesHash, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (toUpload.Count == 0)
                return (batch.Rows, uploadedRows);

            _logger.LogInformation("ShopGold: uploading images of {Count} products ({Connections} FTP connections).", toUpload.Count, _ftp.ImageUploadParallelism);

            var uploaded = new ConcurrentDictionary<ShopGoldRow, bool>();
            var failed = new ConcurrentDictionary<ShopGoldRow, bool>();
            var done = 0;

            await Parallel.ForEachAsync(
                toUpload,
                new ParallelOptions { MaxDegreeOfParallelism = _ftp.ImageUploadParallelism, CancellationToken = ct },
                async (row, token) =>
                {
                    try
                    {
                        await _ftp.UploadProductImagesAsync(row.Images, token);
                        uploaded[row] = true;
                    }
                    catch (Exception ex) when (!token.IsCancellationRequested)
                    {
                        failed[row] = true;
                        _logger.LogError(ex, "Uploading images of {Code} to ShopGold failed - product skipped this cycle.", row.CatalogNumber);
                    }

                    var count = Interlocked.Increment(ref done);
                    if (count % ImageProgressLogInterval == 0 && count < toUpload.Count)
                        _logger.LogInformation("ShopGold: images uploaded for {Done}/{Total} products.", count, toUpload.Count);
                });

            // Kolejność wierszy jak w partii - wypadają tylko te, których zdjęć nie udało się wgrać.
            uploadedRows.AddRange(batch.Rows.Where(uploaded.ContainsKey));
            var result = batch.Rows.Where(row => !failed.ContainsKey(row)).ToList();

            _logger.LogInformation("ShopGold images uploaded for {Uploaded} products, failed for {Failed}.", uploadedRows.Count, failed.Count);

            return (result, uploadedRows);
        }

        private async Task TryDeleteCsvAsync(string fileName, CancellationToken ct)
        {
            try
            {
                await _ftp.DeleteCsvAsync(fileName, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Deleting {File} from the shop's import directory failed.", fileName);
            }
        }
    }
}
