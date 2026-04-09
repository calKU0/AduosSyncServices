using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Helpers;
using AduosSyncServices.Infrastructure.Services;
using Allegro.Aduos.Gaska.ProductsService.Constants;
using Allegro.Aduos.Gaska.ProductsService.Helpers;
using Allegro.Aduos.Gaska.ProductsService.Settings;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Allegro.Aduos.Gaska.ProductsService.Services.Allegro
{
    public class AllegroOfferService : IAllegroOfferService
    {
        private readonly ILogger<AllegroOfferService> _logger;
        private readonly IProductRepository _productRepo;
        private readonly IOfferRepository _offerRepo;
        private readonly IImageRepository _imageRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IParameterRepository _parameterRepo;
        private readonly AllegroApiClient _apiClient;
        private readonly AppSettings _appSettings;
        private readonly PriceSettings _priceSettings;
        private readonly AllegroSettings _allegroSettings;

        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.Preserve,
            WriteIndented = true
        };

        public AllegroOfferService(IProductRepository productRepo, IOfferRepository offerRepo, IParameterRepository parameterRepo, ICategoryRepository categoryRepo, AllegroApiClient apiClient, IOptions<AppSettings> appsettings, IOptions<AllegroSettings> allegroSettings, IOptions<PriceSettings> priceSettings, ILogger<AllegroOfferService> logger, IImageRepository imageRepo)
        {
            _productRepo = productRepo;
            _offerRepo = offerRepo;
            _categoryRepo = categoryRepo;
            _parameterRepo = parameterRepo;
            _apiClient = apiClient;
            _appSettings = appsettings.Value;
            _allegroSettings = allegroSettings.Value;
            _priceSettings = priceSettings.Value;
            _logger = logger;
            _imageRepo = imageRepo;
        }

        public async Task SyncAllegroOffers(CancellationToken ct = default)
        {
            try
            {
                var allOffers = await FetchAllOffers(ct);

                var shippingRates = await _apiClient.GetAsync<ShippingRatesReponse>("/sale/shipping-rates", ct);
                var shippingDict = shippingRates?.ShippingRates?.ToDictionary(s => s.Id, s => s.Name) ?? new Dictionary<string, string>();

                // Split offers into two sets
                var offersWithExternalId = allOffers.Where(o => !string.IsNullOrEmpty(o?.External?.Id)).ToList();
                var offersWithoutExternalId = allOffers.Where(o => string.IsNullOrEmpty(o?.External?.Id)).ToList();

                // Group by External.Id when present
                var latestOffers = offersWithExternalId
                    .GroupBy(o => o.External.Id)
                    .Select(g => g.OrderByDescending(o => o.Id).First())
                    .ToList();

                // Optionally group by Name for those without External.Id
                var groupedByName = offersWithoutExternalId
                    .Where(o => !string.IsNullOrWhiteSpace(o.Name))
                    .GroupBy(o => o.Name)
                    .Select(g => g.OrderByDescending(o => o.Id).First())
                    .ToList();

                // Merge both lists
                latestOffers.AddRange(groupedByName);

                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = ct,
                    MaxDegreeOfParallelism = 25
                };

                // Update shipping info & categories
                foreach (var offer in latestOffers)
                {
                    if (offer.Delivery?.ShippingRates?.Id != null &&
                        shippingDict.TryGetValue(offer.Delivery.ShippingRates.Id, out var name))
                    {
                        offer.Delivery.ShippingRates.Name = name;
                    }

                    if (offer.External?.Id != null && offer.Publication.Status != "ENDED")
                    {
                        await _productRepo.UpdateProductAllegroCategory(offer.External.Id, offer.Category.Id, ct);
                    }
                }

                _logger.LogInformation("Attempting to update database offers.");
                await _offerRepo.UpsertOffers(latestOffers, ct);
                _logger.LogInformation("Fetched and saved {Count} offers from Allegro.", latestOffers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error while fetching and saving offers.");
                throw;
            }
        }

        public async Task SyncAllegroOffersDetails(CancellationToken ct = default)
        {
            try
            {
                var allOffers = await _offerRepo.GetOffersWithoutDetails(ct);

                var offersDetails = new ConcurrentBag<AllegroOfferDetails.Root>();
                int processedCount = 0;

                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = ct,
                    MaxDegreeOfParallelism = 25
                };

                await Parallel.ForEachAsync(allOffers, parallelOptions, async (offer, token) =>
                {
                    try
                    {
                        var detailedOffer = await _apiClient.GetAsync<AllegroOfferDetails.Root>(
                            $"/sale/product-offers/{offer.Id}", token);

                        if (detailedOffer == null)
                            return;

                        detailedOffer.Delivery.ShippingRates.Id = offer.DeliveryName;

                        offersDetails.Add(detailedOffer);

                        var current = Interlocked.Increment(ref processedCount);

                        if (current % 500 == 0)
                        {
                            _logger.LogInformation("Processed {ProcessedCount} / {TotalCount} offers. Details collected so far: {DetailsCount}", current, allOffers.Count, offersDetails.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        var current = Interlocked.Increment(ref processedCount);

                        _logger.LogError(ex, "Exception while fetching details for offer ID {OfferId}. Processed so far: {ProcessedCount}", offer.Id, current);
                    }
                });

                if (!offersDetails.IsEmpty)
                {
                    await _offerRepo.UpsertOfferDetails(offersDetails.ToList(), ct);
                }

                _logger.LogInformation("Finished syncing Allegro offer details. Processed {ProcessedCount} offers. Saved {SavedCount} details.", processedCount, offersDetails.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error while fetching and saving offers.");
            }
        }

        private async Task<List<Offer>> FetchAllOffers(CancellationToken ct)
        {
            const int limit = 100;
            const int maxParallelism = 25;

            var allOffers = new ConcurrentBag<Offer>();

            try
            {
                var firstPage = await _apiClient.GetAsync<OffersResponse>($"/sale/offers?limit={limit}&offset=0", ct);

                if (firstPage?.Offers == null || firstPage.Offers.Count == 0)
                {
                    _logger.LogInformation("No offers found.");
                    return new List<Offer>();
                }

                foreach (var offer in firstPage.Offers)
                    allOffers.Add(offer);

                int totalCount = firstPage.TotalCount;
                int totalPages = (int)Math.Ceiling((double)totalCount / limit);

                _logger.LogInformation("Fetched page 1 with {PageCount} offers. Total offers reported: {TotalCount}. Total pages: {TotalPages}", firstPage.Offers.Count, totalCount, totalPages);

                var offsets = Enumerable.Range(1, totalPages - 1)
                    .Select(page => page * limit)
                    .ToList();

                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = ct,
                    MaxDegreeOfParallelism = maxParallelism
                };

                int processedPages = 1;

                await Parallel.ForEachAsync(offsets, parallelOptions, async (offset, token) =>
                {
                    int pageNumber = (offset / limit) + 1;

                    try
                    {
                        var page = await _apiClient.GetAsync<OffersResponse>(
                            $"/sale/offers?limit={limit}&offset={offset}", token);

                        if (page?.Offers == null)
                            return;

                        foreach (var offer in page.Offers)
                            allOffers.Add(offer);

                        var currentPage = Interlocked.Increment(ref processedPages);

                        _logger.LogInformation("Fetched page {PageNumber} with {PageCount} offers. Progress: {ProcessedPages}/{TotalPages}. Total collected: {TotalCollected}", pageNumber, page.Offers.Count, currentPage, totalPages, allOffers.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception while fetching page {PageNumber}", pageNumber);
                    }
                });

                _logger.LogInformation("Finished fetching offers. Total fetched: {TotalCount}", allOffers.Count);

                return allOffers.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error while fetching offers.");
                throw;
            }
        }

        public async Task UpdateOffers(CancellationToken ct = default)
        {
            try
            {
                var offers = await _offerRepo.GetOffersToUpdate(ct);
                var allegroCategories = (await _categoryRepo.GetAllegroCategories(ct)).ToList();

                // Limit concurrency to avoid rate-limits (tune this number)
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = ct,
                    MaxDegreeOfParallelism = 20
                };

                await Parallel.ForEachAsync(offers, parallelOptions, async (offer, token) =>
                {
                    try
                    {
                        if (offer.Product.AllegroImages == null || !offer.Product.AllegroImages.Any())
                        {
                            var images = await ImportImages(offer.Product, token);

                            if (images == null || !images.Any())
                            {
                                return;
                            }

                            offer.Product.AllegroImages = images;
                        }
                        var offerDto = OfferFactory.PatchOffer(offer, allegroCategories, _appSettings, _allegroSettings, _priceSettings);
                        var response = await _apiClient.SendWithResponseAsync($"/sale/product-offers/{offer.Id}", HttpMethod.Patch, offerDto, token);

                        var body = await response.Content.ReadAsStringAsync(token);

                        await LogAllegroResponse(offer.Product, response, body, true, offer.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception while updating offer for {Code}", offer.Product.Code);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error while updating Allegro offers.");
            }
        }

        public async Task CreateOffers(CancellationToken ct = default)
        {
            try
            {
                var products = await _productRepo.GetProductsToUpload(_appSettings.MinProductStock, _appSettings.MinProductPriceNet, ct);
                var allegroCategories = (await _categoryRepo.GetAllegroCategories(ct)).ToList();

                if (products == null || !products.Any())
                {
                    _logger.LogInformation("No products to upload.");
                    return;
                }

                await Parallel.ForEachAsync(products, new ParallelOptions
                {
                    CancellationToken = ct,
                    MaxDegreeOfParallelism = 20
                },
                async (product, token) =>
                {
                    try
                    {
                        product.AllegroImages = await ImportImages(product, token);
                        if (product.AllegroImages == null || !product.AllegroImages.Any())
                        {
                            return;
                        }
                        var offer = OfferFactory.BuildOffer(product, allegroCategories, _appSettings, _allegroSettings, _priceSettings);
                        var response = await _apiClient.SendWithResponseAsync("/sale/product-offers", HttpMethod.Post, offer, token);
                        var body = await response.Content.ReadAsStringAsync();
                        await LogAllegroResponse(product, response, body);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception while creating offer for {Code}", product.Code);
                    }
                });

                _logger.LogInformation("Finished creating Allegro offers.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error while creating Allegro offers.");
            }
        }

        private async Task LogAllegroResponse(Product product, HttpResponseMessage response, string body, bool isUpdate = false, string offerId = null)
        {
            var action = isUpdate ? "updated" : "created";

            switch ((int)response.StatusCode)
            {
                case 200:
                    _logger.LogInformation($"Offer {action} successfully for {product.Code}");
                    await _imageRepo.MarkImagesAsConnectedAsync(product.Id, new CancellationToken());
                    break;

                case 201:
                    _logger.LogInformation($"Offer {action} successfully for {product.Code}");
                    await _imageRepo.MarkImagesAsConnectedAsync(product.Id, new CancellationToken());
                    break;

                case 202:
                    _logger.LogInformation($"Offer {action} successfully but still processing for {product.Code}");
                    await _imageRepo.MarkImagesAsConnectedAsync(product.Id, new CancellationToken());
                    break;

                case 400:
                case 422:
                case 433:
                    await _imageRepo.DeleteNotConnectedImages(product.Id, CancellationToken.None);
                    await LogAllegroErrors(product, response, body, isUpdate, offerId);
                    break;

                case 401:
                    await _imageRepo.DeleteNotConnectedImages(product.Id, CancellationToken.None);
                    _logger.LogError($"Unauthorized (401). Check token for product {product.Code} when {action} offer.");
                    break;

                case 403:
                    await _imageRepo.DeleteNotConnectedImages(product.Id, CancellationToken.None);
                    _logger.LogError($"Forbidden (403). No permission for {action} offer for {product.Code}.");
                    break;

                case 404:
                    await _imageRepo.DeleteNotConnectedImages(product.Id, CancellationToken.None);
                    _logger.LogWarning("Offer not found in Allegro. Deleting from database.");
                    await _offerRepo.DeleteOffer(offerId, CancellationToken.None);
                    break;
                case 429:
                    break;

                default:
                    await _imageRepo.DeleteNotConnectedImages(product.Id, CancellationToken.None);
                    _logger.LogError($"Unexpected status {(int)response.StatusCode} ({response.StatusCode}) while {action} offer for {product.Code}. Response: {body}");
                    break;
            }
        }

        private async Task LogAllegroErrors(Product product, HttpResponseMessage response, string body, bool isUpdate = false, string offerId = null)
        {
            var action = isUpdate ? "updating" : "creating";
            try
            {
                var errorResponse = JsonSerializer.Deserialize<AllegroErrorResponse>(body, _options);
                if (errorResponse?.Errors != null)
                {
                    foreach (var err in errorResponse.Errors)
                    {
                        // Special handling for category mismatch
                        if (((err.Code?.Contains("ProductConstraintViolationException", StringComparison.OrdinalIgnoreCase) == true && (err.UserMessage ?? "").Contains("kategorii produktu", StringComparison.OrdinalIgnoreCase)) || err.Code?.Contains("CATEGORY_MISMATCH", StringComparison.OrdinalIgnoreCase) == true) && !string.IsNullOrEmpty(err.UserMessage))
                        {
                            var correctCategoryId = ExtractCorrectCategoryId(err.UserMessage);
                            if (!string.IsNullOrEmpty(correctCategoryId))
                            {
                                await _productRepo.UpdateProductAllegroCategory(product.Id, Convert.ToInt32(correctCategoryId), CancellationToken.None);
                                _logger.LogInformation("Updated category for {Code} to {CategoryId}", product.Code, correctCategoryId);
                            }
                        }
                        else if (err.UserMessage.Contains("nieznany producent"))
                        {
                            await _parameterRepo.UpdateParameter(product.Id, 127415, "JAG", CancellationToken.None);
                            await _parameterRepo.UpdateParameter(product.Id, 247835, "JAG", CancellationToken.None);
                        }
                        else if (
                            (err.Code == "PARAMETER_MISMATCH" && !string.IsNullOrEmpty(err.UserMessage)) ||
                            (err.Code == "ProductConstraintViolationException.DataIntegrity" &&
                             err.Message.Contains("Incorrect value of the") &&
                             err.Message.Contains("parameter for the offered product"))
                        )
                        {
                            _logger.LogInformation("Offer {Action} error for {Name}: Code={Code}, Message={Message}", action, product.Code, err.Code, err.Message);
                            // Try to extract from either UserMessage or Message
                            var sourceMessage = !string.IsNullOrEmpty(err.UserMessage) ? err.UserMessage : err.Message;

                            var correctValue = ExtractCorrectParameterValue(sourceMessage);
                            var parameterId = ExtractParameterIdFromConstraintMessage(err.Message);

                            if (!string.IsNullOrEmpty(parameterId) && !string.IsNullOrEmpty(correctValue))
                            {
                                await _parameterRepo.UpdateParameter(product.Id, Convert.ToInt32(parameterId), correctValue, CancellationToken.None);
                                _logger.LogInformation("Updated parameter {ParameterId} for {Code} to '{CorrectValue}'", parameterId, product.Code, correctValue);
                            }
                        }
                        else if (err.Code == "ProductValidationException"
                             && !string.IsNullOrEmpty(err.UserMessage)
                             && err.UserMessage.Contains("Tworzenie produktu z wartością", StringComparison.OrdinalIgnoreCase)
                             && err.UserMessage.Contains("parametrze Producent części", StringComparison.OrdinalIgnoreCase))
                        {
                            await _parameterRepo.UpdateParameter(product.Id, 127415, "JAG", CancellationToken.None);
                            await _parameterRepo.UpdateParameter(product.Id, 247835, "JAG", CancellationToken.None);

                            _logger.LogInformation("Updated parameter Producent części to 'JAG' for {Code}", product.Code);
                        }
                        else if (err.Code == "Code=ConstraintViolationException.ValidProductization")
                        {
                            // TODO
                        }
                        else if (err.UserMessage.Contains(@"Podany adres obrazka jest nieprawidłowy."))
                        {
                            await _imageRepo.DeleteProductImagesAsync(product.Id, CancellationToken.None);
                        }
                        else if (err.UserMessage.Contains(@"bez wybierania wartości niejednoznacznej"))
                        {
                        }
                        else if (err.Message.Contains(@"The type of this ""Compatible with"" "))
                        {
                            await _productRepo.UpdateCompatibilitySet(product.Id, false, CancellationToken.None);
                        }
                        else if (err.Code == "ParameterNameNotFoundException" && !string.IsNullOrEmpty(err.UserMessage))
                        {
                            var parameterName = ExtractParameterNameFromNotFoundMessage(err.UserMessage);

                            if (!string.IsNullOrEmpty(parameterName))
                            {
                                await _parameterRepo.DeleteParameter(parameterName, product.Id, CancellationToken.None);

                                _logger.LogInformation(
                                    "Deleted parameter '{ParameterName}' for product {Code} because it does not exist in category",
                                    parameterName,
                                    product.Code);
                            }
                        }
                        else if (err.Code == "OfferNotFoundException" && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger.LogWarning("Offer not found in Allegro. Deleting from database.");
                            await _offerRepo.DeleteOffer(offerId, CancellationToken.None);
                        }
                        else
                        {
                            _logger.LogError("Offer {Action} error for {ProductCode}: Code={Code}, Message={Message}", action, product.Code, err.Code, err.Message);
                        }
                    }
                }
                else
                {
                    _logger.LogError($"Offer {action} error {response.StatusCode} for {product.Code}: {body}");
                }
            }
            catch (Exception exParse)
            {
                if (body.Contains(@"The type of this ""Compatible with"" "))
                {
                    await _productRepo.UpdateCompatibilitySet(product.Id, false, CancellationToken.None);
                }
                _logger.LogError(exParse, $"Failed to parse Allegro error ({response.StatusCode}) while {action} offer for {product.Code}. Body={body}");
            }
        }

        private string ExtractParameterNameFromNotFoundMessage(string message)
        {
            const string prefix = "Parameter ";
            const string suffix = " not found";

            var start = message.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            var end = message.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);

            if (start >= 0 && end > start)
            {
                start += prefix.Length;
                return message.Substring(start, end - start).Trim();
            }

            return null;
        }
        private string ExtractCorrectCategoryId(string message)
        {
            // Try to match specifically "produktu (123456)" first (preferred pattern)
            var correctMatch = Regex.Match(message, @"produktu\s*\((\d+)\)", RegexOptions.IgnoreCase);
            if (correctMatch.Success)
                return correctMatch.Groups[1].Value;

            // Fallback: if message contains multiple category IDs, assume the last one is correct
            var allMatches = Regex.Matches(message, @"\((\d+)\)");
            if (allMatches.Count > 1)
                return allMatches[^1].Groups[1].Value;

            return allMatches.Count == 1 ? allMatches[0].Groups[1].Value : null;
        }

        private string ExtractCorrectParameterValue(string message)
        {
            // Matches "is: "JAG"", "to: "JAG"", or Polish "to: "JAG""
            // Look for the last quoted string in the message
            var matches = Regex.Matches(message, @"[""“‘`]([^""”’`]+)[""“‘`]");
            if (matches.Count > 0)
            {
                return matches[^1].Groups[1].Value; // Take the last quoted value (usually the "correct" one)
            }
            return null;
        }

        private string ExtractParameterIdFromConstraintMessage(string message)
        {
            // Matches either "id: 123" or "(123)"
            var match = Regex.Match(message, @"(?:id:\s*|[(])(\d+)[)]", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
        private async Task<List<AllegroImages>> ImportImages(Product product, CancellationToken ct)
        {
            var imageResults = new ConcurrentBag<(string FileName, string Url)>();

            if (!Directory.Exists(ServiceConstants.ImagesFolder))
            {
                _logger.LogWarning("Images folder not found: {Path}", ServiceConstants.ImagesFolder);
                return new List<AllegroImages>();
            }

            var imageFiles = ImageHelper.GetImageFiles(ServiceConstants.ImagesFolder, product.Id);

            if (!imageFiles.Any())
            {
                return new List<AllegroImages>();
            }

            // Upload product images in parallel
            await Parallel.ForEachAsync(
                imageFiles,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 3,
                    CancellationToken = ct
                },
                async (filePath, token) =>
                {
                    try
                    {
                        var imageBytes = await File.ReadAllBytesAsync(filePath, token);
                        var validatedBytes = Utils.EnsureImageMinSize(imageBytes);

                        if (validatedBytes == null)
                        {
                            _logger.LogWarning("Image too small or invalid: {File}", filePath);
                            return;
                        }

                        var contentType = Utils.GetContentTypeFromPath(filePath);

                        var uploadResult = await _apiClient.PostAsync<AllegroImageResponse>("/sale/images", validatedBytes, token, contentType);

                        if (!string.IsNullOrWhiteSpace(uploadResult?.Location))
                        {
                            imageResults.Add((Path.GetFileName(filePath), uploadResult.Location));

                            _logger.LogInformation("Uploaded image {File} -> {Url}", Path.GetFileName(filePath), uploadResult.Location);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading image {File}", Path.GetFileName(filePath));
                    }
                });

            // Sort uploaded product images alphabetically
            var orderedUrls = imageResults
                .OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Url)
                .ToList();

            // Upload logo LAST
            var logoPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources",
                "Images",
                "logo.jpg");

            try
            {
                if (File.Exists(logoPath))
                {
                    var logoBytes = await File.ReadAllBytesAsync(logoPath, ct);
                    var logoResult = await _apiClient.PostAsync<AllegroImageResponse>("/sale/images", logoBytes, ct, "image/jpeg");

                    if (!string.IsNullOrWhiteSpace(logoResult?.Location))
                    {
                        orderedUrls.Add(logoResult.Location);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload logo image.");
            }

            _logger.LogInformation("Imported {Count} images for product {Code}", orderedUrls.Count, product.Code);

            var result = new List<AllegroImages>(orderedUrls.Count);

            foreach (var url in orderedUrls)
            {
                var imageId = await _imageRepo.AddImageAsync(product.Id, url, ct);

                result.Add(new AllegroImages
                {
                    Id = imageId,
                    ProductId = product.Id,
                    Url = url,
                    Connected = false
                });
            }

            return result;
        }
    }
}