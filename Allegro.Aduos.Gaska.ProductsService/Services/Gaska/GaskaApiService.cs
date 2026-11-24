using AduosSyncServices.Contracts.Clients;
using AduosSyncServices.Contracts.Data;
using AduosSyncServices.Contracts.DTOs.GaskaApi;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.Infrastructure.Helpers;
using Allegro.Aduos.Gaska.ProductsService.Constants;
using Allegro.Aduos.Gaska.ProductsService.Services.Gaska.Interfaces;
using Allegro.Aduos.Gaska.ProductsService.Settings;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Allegro.Aduos.Gaska.ProductsService.Services.GaskaApiService
{
    public class GaskaApiService : IGaskaApiService
    {
        // Shared client for image downloads: creating a new HttpClient per product/batch leaks sockets
        // (each instance holds its connections in TIME_WAIT after disposal) and a long-running worker
        // syncing thousands of products can exhaust the ephemeral port range.
        private static readonly HttpClient ImageHttpClient = new();

        private readonly ILogger<GaskaApiService> _logger;
        private readonly IProductRepository _productRepo;
        private readonly IImageRepository _imageRepo;
        private readonly IGaskaApiClient _gaskaApiClient;
        private readonly ISyncCategoryRepository _syncCategoryRepo;
        private readonly List<int> _categoriesIds;
        private IOptions<GaskaApiCredentials> _apiSettings;
        private readonly IOptions<AppSettings> _appSettings;

        public GaskaApiService(IProductRepository productRepo, IImageRepository imageRepo, IGaskaApiClient gaskaApiClient, ISyncCategoryRepository syncCategoryRepo, IOptions<GaskaApiCredentials> apiSettings, IOptions<AppSettings> appSettings, ILogger<GaskaApiService> logger)
        {
            _productRepo = productRepo;
            _imageRepo = imageRepo;
            _gaskaApiClient = gaskaApiClient;
            _syncCategoryRepo = syncCategoryRepo;
            _categoriesIds = (appSettings.Value.CategoriesId ?? new List<int>())
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            _apiSettings = apiSettings;
            _appSettings = appSettings;
            _logger = logger;
        }

        public async Task SyncProducts(CancellationToken ct = default)
        {
            bool hasErrors = false;
            var syncedIntegrationIds = new HashSet<int>();

            // Drzewo kategorii - lista wyboru kategorii w ServicesManagerze (Allegro i ShopGold).
            await SyncCategoryTreeAsync(ct);

            // Kategorie innych kanałów (ShopGold) też trzeba pobrać do bazy - produkty z Gąski pobiera
            // tylko ten serwis. Na Allegro trafiają jednak wyłącznie produkty z kategorii Allegro
            // (filtr po powiązaniach ProductSyncCategories w procedurach ofert).
            var categoriesToFetch = await GetCategoriesToFetchAsync(ct);

            if (!_categoriesIds.Any())
            {
                _logger.LogInformation("No Allegro categories configured. Syncing ALL products.");
                var result = await SyncProductsByCategory(null, ct);
                hasErrors = hasErrors || !result.Success;
                syncedIntegrationIds.UnionWith(result.IntegrationIds);
            }

            foreach (var categoryId in categoriesToFetch)
            {
                var result = await SyncProductsByCategory(categoryId, ct);
                hasErrors = hasErrors || !result.Success;
                syncedIntegrationIds.UnionWith(result.IntegrationIds);

                // Powiązania zmieniamy tylko dla kategorii pobranej w całości - niepełne pobranie
                // odebrałoby produktom kategorię, w której nadal są.
                if (result.Success)
                    await SaveCategoryLinksAsync(categoryId, result.IntegrationIds, ct);
            }

            if (hasErrors)
            {
                _logger.LogWarning("Errors occurred during product sync. Archiving skipped.");
                return;
            }

            await DeleteLinksOfRemovedCategoriesAsync(categoriesToFetch, ct);

            if (!syncedIntegrationIds.Any())
            {
                _logger.LogWarning("No products were fetched from API. Archiving skipped to avoid accidental archiving.");
                return;
            }

            var archivedCount = await _productRepo.ArchiveProductsNotInIntegrationIdsAsync(syncedIntegrationIds, ct);
            _logger.LogInformation("Archived {ArchivedCount} products not found in API sync.", archivedCount);

            // A product whose code changed in Gąska gets re-inserted under the new code (upsert matches
            // on Code), leaving a stale old-code row with the SAME IntegrationId - which the missing-id
            // archiving above can never catch. Archive the older duplicate(s), keep the newest row.
            var duplicateArchivedCount = await _productRepo.ArchiveOlderDuplicateProductsAsync(ct);
            if (duplicateArchivedCount > 0)
                _logger.LogInformation("Archived {Count} older duplicate products (same IntegrationId, different code).", duplicateArchivedCount);

            var deletedCount = await _productRepo.DeleteArchivedProductsWithEndedOffersAsync(ct);
            if (deletedCount > 0)
                _logger.LogInformation("Deleted {DeletedCount} archived products whose Allegro offer is ENDED.", deletedCount);
        }

        /// <summary>
        /// Pełne drzewo kategorii Gąski z /categories - jedno zapytanie na cykl. Błąd nie przerywa
        /// pobierania produktów: lista wyboru zostaje po prostu z poprzedniego cyklu.
        /// </summary>
        private async Task SyncCategoryTreeAsync(CancellationToken ct)
        {
            try
            {
                var response = await _gaskaApiClient.GetCategories("pl", ct);

                if (response.Result != 0)
                {
                    _logger.LogError("Gąska API returned an error for categories: {Message}", response.Message ?? "empty response");
                    return;
                }

                var count = await _syncCategoryRepo.UpsertGaskaCategoryTreeAsync(
                    (response.Categories ?? new List<ApiCategory>()).Select(c => (c.Id, c.ParentID, c.Name ?? string.Empty)), ct);

                _logger.LogInformation("Gąska category tree synchronized: {Count} categories.", count);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Synchronizing the Gąska category tree failed.");
            }
        }

        /// <summary>
        /// Zapisuje kategorie Allegro do wspólnej tabeli i zwraca sumę kategorii wszystkich kanałów.
        /// Przy błędzie bazy pobieramy tylko kategorie Allegro - jak przed wprowadzeniem kanałów.
        /// </summary>
        private async Task<List<int>> GetCategoriesToFetchAsync(CancellationToken ct)
        {
            try
            {
                await _syncCategoryRepo.ReplaceChannelCategoriesAsync(SyncChannels.Allegro, _categoriesIds, ct);

                var allChannels = await _syncCategoryRepo.GetAllChannelCategoriesAsync(ct);
                var otherChannels = allChannels
                    .Where(c => !string.Equals(c.Channel, SyncChannels.Allegro, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.CategoryId)
                    .Where(id => id != 0 && !_categoriesIds.Contains(id))
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList();

                if (otherChannels.Any())
                {
                    _logger.LogInformation(
                        "Fetching also {Count} categories configured only in other channels (not listed on Allegro): {Categories}",
                        otherChannels.Count, string.Join(", ", otherChannels));
                }

                return _categoriesIds.Concat(otherChannels).ToList();
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error while reading categories of other sales channels. Fetching Allegro categories only.");
                return _categoriesIds.ToList();
            }
        }

        private async Task SaveCategoryLinksAsync(int categoryId, IEnumerable<int> integrationIds, CancellationToken ct)
        {
            try
            {
                await _syncCategoryRepo.ReplaceProductLinksForCategoryAsync(categoryId, integrationIds, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Saving product links for category {CategoryId} failed.", categoryId);
            }
        }

        private async Task DeleteLinksOfRemovedCategoriesAsync(IEnumerable<int> fetchedCategories, CancellationToken ct)
        {
            try
            {
                var deleted = await _syncCategoryRepo.DeleteProductLinksNotInAsync(fetchedCategories, ct);
                if (deleted > 0)
                    _logger.LogInformation("Removed {Count} product links to categories no longer configured in any channel.", deleted);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Removing product links of unconfigured categories failed.");
            }
        }

        private async Task<(bool Success, HashSet<int> IntegrationIds)> SyncProductsByCategory(int? categoryId, CancellationToken ct)
        {
            int page = 1;
            bool hasMore = true;
            var integrationIds = new HashSet<int>();

            while (hasMore)
            {
                try
                {
                    var response = await _gaskaApiClient.GetProducts(new GaskaGetProductsRequest
                    {
                        CategoryId = categoryId,
                        Page = page,
                        PageSize = _apiSettings.Value.ProductsPerPage,
                        Language = "pl"
                    }, ct);

                    if (response == null)
                    {
                        _logger.LogError("API error while fetching page {Page} for category {Category}.", page, categoryId ?? 0);
                        return (false, integrationIds);
                    }

                    if (response?.Products == null || response.Products.Count == 0)
                    {
                        _logger.LogInformation("No products returned on page {Page} for category {Category}. Stopping fetch.", page, categoryId ?? 0);
                        break;
                    }

                    List<Product> productsToUpsert = new List<Product>();
                    foreach (var product in response.Products)
                    {
                        productsToUpsert.Add(MapToProduct(product));

                        if (product.Id > 0)
                            integrationIds.Add(product.Id);
                    }

                    await _productRepo.UpsertProductsBatchAsync(productsToUpsert, ct);

                    _logger.LogInformation("Fetched {ProductCount} products for category {Category} on page {Page}.", response.Products.Count, categoryId ?? 0, page);

                    // Stop if fewer products than page size minus buffer
                    if (response.Products.Count < _apiSettings.Value.ProductsPerPage - 10)
                    {
                        _logger.LogInformation("Fetched fewer products than expected on page {Page} for category {Category}. Ending paging.", page, categoryId ?? 0);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error while getting products from page {page} for category {categoryId ?? 0}.");
                    return (false, integrationIds);
                }
                finally
                {
                    page++;
                    await Task.Delay(TimeSpan.FromSeconds(_apiSettings.Value.ProductsInterval), ct);
                }
            }

            return (true, integrationIds);
        }

        public async Task<bool> SyncProductDetails(CancellationToken ct = default)
        {
            List<int> productsToUpdate;

            try
            {
                productsToUpdate = await _productRepo.GetProductsForDetailUpdate(_apiSettings.Value.ProductPerDay, ct);

                if (!productsToUpdate.Any() || productsToUpdate.Count < _apiSettings.Value.ProductPerDay)
                {
                    var remainingSlots = _apiSettings.Value.ProductPerDay - productsToUpdate.Count;

                    if (remainingSlots > 0)
                    {
                        var productsChanged = await GetProductsChanged(DateTime.Now.AddDays(-_appSettings.Value.ProductsChangedDaysBack), ct);
                        _logger.LogInformation("Found {Count} products changed", productsChanged?.Count ?? 0);
                        if (productsChanged != null && productsChanged.Any())
                        {
                            productsToUpdate.AddRange(productsChanged.Take(remainingSlots));
                        }
                    }
                }

                if (!productsToUpdate.Any())
                {
                    _logger.LogInformation("No products found for detail update today.");
                    return true;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting products to update details from database.");
                return false;
            }

            foreach (var productId in productsToUpdate)
            {
                try
                {
                    var response = await _gaskaApiClient.GetProduct(productId, "pl", ct);

                    if (response == null)
                    {
                        _logger.LogError("API error while fetching product details for {Id}.", productId);
                        continue;
                    }

                    if (response?.Product == null)
                    {
                        _logger.LogWarning("Product details returned null for {Id}. Skipping update.", productId);
                        continue;
                    }

                    var existingProduct = await _productRepo.GetProductByIntegrationIdAsync(productId, ct);
                    if (existingProduct == null)
                    {
                        _logger.LogInformation("Product with IntegrationId {Id} not found in database. Skipping update.", productId);
                        continue;
                    }

                    await SaveProductImagesAsync(response.Product, existingProduct.Id, ct);
                    await _productRepo.UpsertProductAsync(MapToProduct(existingProduct, response.Product), ct);

                    _logger.LogInformation("Successfully fetched and updated details of product {ProductCode}.", existingProduct.Code);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while updating product {Id}.", productId);
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(_apiSettings.Value.ProductInterval), ct);
                }
            }

            return true;
        }

        private async Task<List<int>?> GetProductsChanged(DateTime dateFrom, CancellationToken ct)
        {
            var response = await _gaskaApiClient.GetProductsChanged(dateFrom, ct);

            if (response == null)
            {
                _logger.LogError("API error while fetching products changed from {DateFrom}", dateFrom);
                return null;
            }

            if (response?.Products == null || !response.Products.Any())
            {
                _logger.LogWarning("No products changed from {DateFrom}.", dateFrom);
                return null;
            }

            return response.Products.Select(p => p.TwrId).ToList();
        }

        private async Task SaveProductImagesAsync(ApiProduct product, int productId, CancellationToken ct)
        {
            if (product.Images == null || !product.Images.Any())
                return;

            var urls = product.Images
                .Where(i => !string.IsNullOrWhiteSpace(i.Url))
                .Select(i => i.Url)
                .ToList();

            if (!urls.Any())
                return;

            var savedPaths = await ImageHelper.SaveImagesAsync(ImageHttpClient, urls, productId, ServiceConstants.ImagesFolder, ct);
            await _imageRepo.DeleteProductImagesAsync(productId, ct);
            if (savedPaths == null || !savedPaths.Any())
                _logger.LogWarning("Failed to save images for product {Code}", product.CodeGaska);
        }

        private static Product MapToProduct(ApiProducts product)
        {
            return new Product
            {
                Code = product.CodeGaska,
                CustomerCode = product.CodeCustomer,
                Name = product.Name,
                Description = product.Description + " " + product.TechnicalDetails,
                Ean = product.Ean,
                Weight = product.GrossWeight,
                SupplierName = product.SupplierName,
                SupplierLogo = product.SupplierLogo,
                InStock = product.InStock,
                Unit = product.Unit,
                CurrencyPrice = product.CurrencyPrice,
                PriceNet = product.NetPrice,
                PriceGross = product.GrossPrice,
                DeliveryType = product.DeliveryType,
                IntegrationId = product.Id
            };
        }

        private static Product MapToProduct(Product existing, ApiProduct product)
        {
            return new Product
            {
                Id = existing.Id,
                Code = product.CodeGaska ?? existing.Code,
                CustomerCode = product.CodeCustomer ?? existing.CustomerCode,
                Name = product.Name ?? existing.Name,
                Description = existing.Description,
                Ean = existing.Ean,
                Weight = existing.Weight,
                SupplierName = product.SupplierName ?? existing.SupplierName,
                SupplierLogo = product.SupplierLogo ?? existing.SupplierLogo,
                Substitutes = product.CrossNumbers != null
                    ? string.Join(',', product.CrossNumbers.Select(c => c.CrossNumber).Where(c => !string.IsNullOrWhiteSpace(c)))
                    : existing.Substitutes,
                InStock = product.InStock,
                Unit = product.Packages?.Where(p => p.PackRequired == 1).Select(p => p.PackUnit).FirstOrDefault() ?? existing.Unit,
                CurrencyPrice = product.CurrencyPrice ?? existing.CurrencyPrice,
                Package = product.Packages?.Where(p => p.PackRequired == 1).Select(p => Convert.ToDecimal(p.PackQty)).FirstOrDefault() ?? 1,
                PriceNet = product.PriceNet,
                PriceGross = product.PriceGross,
                DeliveryType = product.DeliveryType,
                IntegrationId = product.Id,
                Packages = product.Packages?.Select(p => new ProductPackage
                {
                    PackUnit = p.PackUnit,
                    PackQty = p.PackQty,
                    PackNettWeight = p.PackNettWeight,
                    PackGrossWeight = p.PackGrossWeight,
                    PackEan = p.PackEan,
                    PackRequired = p.PackRequired
                }).ToList() ?? new List<ProductPackage>(),
                Applications = product.Applications?.Select(a => new ProductApplication
                {
                    ApplicationId = a.Id,
                    ParentID = a.ParentID,
                    Name = a.Name
                }).ToList() ?? new List<ProductApplication>(),
                Specifications = MapSpecifications(product.Parameters),
                Categories = MapCategories(product.Categories)
            };
        }

        private static List<ProductSpecification> MapSpecifications(IEnumerable<ApiParameter>? parameters)
        {
            return parameters?
                .Where(p => !string.IsNullOrWhiteSpace(p.AttributeName))
                .Select(p =>
                {
                    var (name, unit) = SplitAttributeNameAndUnit(p.AttributeName);

                    return new ProductSpecification
                    {
                        Name = name,
                        Value = p.AttributeValue?.Trim() ?? string.Empty,
                        UnitName = unit
                    };
                })
                .ToList() ?? new List<ProductSpecification>();
        }

        private static (string Name, string Unit) SplitAttributeNameAndUnit(string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
                return (string.Empty, string.Empty);

            var trimmed = attributeName.Trim();

            // Match unit in trailing parentheses, e.g. "Szerokość (mm)" -> ("Szerokość", "mm")
            var match = Regex.Match(trimmed, @"^(?<name>.*)\s*\((?<unit>[^()]*)\)\s*$");
            if (!match.Success)
                return (trimmed, string.Empty);

            var unit = match.Groups["unit"].Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(unit))
                return (trimmed, string.Empty);

            var name = match.Groups["name"].Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                name = trimmed;

            return (name, unit);
        }

        private static List<ProductCategory> MapCategories(IEnumerable<ApiCategory>? categories)
        {
            if (categories == null)
                return new List<ProductCategory>();

            var categoryList = categories.ToList();
            var parentIds = categoryList.Select(c => c.ParentID).ToHashSet();
            var leafCategories = categoryList.Where(c => !parentIds.Contains(c.Id)).ToList();
            var categoryLookup = categoryList
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var result = new List<ProductCategory>();

            foreach (var category in leafCategories)
            {
                var name = BuildCategoryName(category, categoryLookup);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                result.Add(new ProductCategory { Name = name });
            }

            return result
                .GroupBy(c => c.Name)
                .Select(g => g.First())
                .ToList();
        }

        private static string BuildCategoryName(ApiCategory category, IReadOnlyDictionary<int, ApiCategory> lookup)
        {
            var parts = new Stack<string>();
            var visited = new HashSet<int>();
            var current = category;

            while (current != null && visited.Add(current.Id))
            {
                if (!string.IsNullOrWhiteSpace(current.Name))
                    parts.Push(current.Name.Trim());

                if (current.ParentID == 0 || !lookup.TryGetValue(current.ParentID, out var parent))
                    break;

                current = parent;
            }

            return string.Join(" > ", parts);
        }
    }
}