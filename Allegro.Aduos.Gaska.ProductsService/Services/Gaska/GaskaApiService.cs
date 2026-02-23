using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.Infrastructure.Helpers;
using Allegro.Aduos.Gaska.ProductsService.Constants;
using Allegro.Aduos.Gaska.ProductsService.DTOs;
using Allegro.Aduos.Gaska.ProductsService.Services.Gaska.Interfaces;
using Allegro.Aduos.Gaska.ProductsService.Settings;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Allegro.Aduos.Gaska.ProductsService.Services.GaskaApiService
{
    public class GaskaApiService : IGaskaApiService
    {
        private readonly ILogger<GaskaApiService> _logger;
        private readonly IProductRepository _productRepo;
        private readonly HttpClient _http;
        private readonly List<int> _categoriesIds;
        private IOptions<GaskaApiCredentials> _apiSettings;

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public GaskaApiService(IProductRepository productRepo, HttpClient http, IOptions<GaskaApiCredentials> apiSettings, IOptions<AppSettings> appSettings, ILogger<GaskaApiService> logger)
        {
            _productRepo = productRepo;
            _http = http;
            _categoriesIds = appSettings.Value.CategoriesId?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var val) ? val : (int?)null)
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToList()
                ?? new List<int>();

            _apiSettings = apiSettings;
            _logger = logger;
        }

        public async Task SyncProducts(CancellationToken ct = default)
        {
            bool hasErrors = false;

            if (_categoriesIds == null || !_categoriesIds.Any())
            {
                _logger.LogInformation("No categories configured. Syncing ALL products.");
                await SyncProductsByCategory(null, ct);
            }
            else
            {
                foreach (var categoryId in _categoriesIds)
                {
                    await SyncProductsByCategory(categoryId, ct);
                }
            }

            if (hasErrors)
            {
                _logger.LogWarning("Errors occurred during product sync. Archiving skipped.");
                return;
            }
        }

        private async Task SyncProductsByCategory(int? categoryId, CancellationToken ct)
        {
            int page = 1;
            bool hasMore = true;

            while (hasMore)
            {
                try
                {
                    var url = categoryId.HasValue
                        ? $"/products?category={categoryId}&page={page}&perPage={_apiSettings.Value.ProductsPerPage}&lng=pl"
                        : $"/products?page={page}&perPage={_apiSettings.Value.ProductsPerPage}&lng=pl";

                    var response = await _http.GetAsync(url, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("API error while fetching page {Page} for category {Category}: {StatusCode}", page, categoryId ?? 0, response.StatusCode);
                        break;
                    }

                    var json = await response.Content.ReadAsStringAsync(ct);
                    var apiResponse = JsonSerializer.Deserialize<ProductsResponse>(json, _jsonOptions);

                    if (apiResponse?.Products == null || apiResponse.Products.Count == 0)
                    {
                        _logger.LogInformation("No products returned on page {Page} for category {Category}. Stopping fetch.", page, categoryId ?? 0);
                        break;
                    }

                    foreach (var product in apiResponse.Products)
                    {
                        await _productRepo.UpsertProductAsync(MapToProduct(product), ct);
                    }

                    _logger.LogInformation("Fetched {ProductCount} products for category {Category} on page {Page}.", apiResponse.Products.Count, categoryId ?? 0, page);

                    // Stop if fewer products than page size minus buffer
                    if (apiResponse.Products.Count < _apiSettings.Value.ProductsPerPage - 10)
                    {
                        _logger.LogInformation("Fetched fewer products than expected on page {Page} for category {Category}. Ending paging.", page, categoryId ?? 0);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error while getting products from page {page} for category {categoryId ?? 0}.");
                    break;
                }
                finally
                {
                    page++;
                    await Task.Delay(TimeSpan.FromSeconds(_apiSettings.Value.ProductsInterval), ct);
                }
            }
        }

        public async Task SyncProductDetails(CancellationToken ct = default)
        {
            List<Product> productsToUpdate;

            try
            {
                productsToUpdate = await _productRepo.GetProductsForDetailUpdate(_apiSettings.Value.ProductPerDay, ct);
                if (!productsToUpdate.Any()) return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting products to update details from database.");
                return;
            }

            foreach (var product in productsToUpdate)
            {
                try
                {
                    var url = $"/product?id={product.IntegrationId}&lng=pl";
                    var response = await _http.GetAsync(url, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("API error while fetching product details for {ProductCode}. Response Status: {StatusCode}", product.Code, response.StatusCode);
                        continue;
                    }

                    var json = await response.Content.ReadAsStringAsync(ct);
                    var apiResponse = JsonSerializer.Deserialize<ProductResponse>(json, _jsonOptions);

                    if (apiResponse?.Product == null)
                    {
                        _logger.LogWarning("Product details returned null for {ProductCode}. Skipping update.", product.Code);
                        continue;
                    }

                    await SaveProductImagesAsync(apiResponse.Product, ct);
                    await _productRepo.UpsertProductAsync(MapToProduct(product, apiResponse.Product), ct);

                    _logger.LogInformation("Successfully fetched and updated details of product {ProductCode}.", product.Code);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while updating product {ProductCode}.", product.Code);
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(_apiSettings.Value.ProductInterval), ct);
                }
            }
        }

        private async Task SaveProductImagesAsync(ApiProduct product, CancellationToken ct)
        {
            if (product.Images == null || !product.Images.Any())
                return;

            foreach (var image in product.Images)
            {
                if (string.IsNullOrWhiteSpace(image?.Url))
                    continue;

                // Validate URL
                if (!Uri.TryCreate(image.Url, UriKind.Absolute, out var uriResult) || (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    _logger.LogWarning("Invalid image URL for product {Code}: {Url}", product.CodeGaska, image.Url);
                    continue;
                }

                try
                {
                    var savedPath = await ImageHelper.SaveImageAsync(_http, image.Url, product.Id, ServiceConstants.ImagesFolder, ct);

                    if (string.IsNullOrWhiteSpace(savedPath))
                        _logger.LogWarning("Failed to save image for product {Code}. Url: {Url}", product.CodeGaska, image.Url);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save image for product {Code}. Url: {Url}", product.CodeGaska, image.Url);
                }
            }
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