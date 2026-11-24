using AduosSyncServices.Contracts.Clients;
using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Helpers;
using Allegro.Aduos.Gaska.ProductsService.Constants;
using Allegro.Aduos.Gaska.ProductsService.Helpers;
using AduosSyncServices.Contracts.Settings;
using Allegro.Aduos.Gaska.ProductsService.Settings;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;
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
        private readonly IAllegroApiClient _apiClient;
        private readonly AppSettings _appSettings;
        private readonly PriceSettings _priceSettings;
        private readonly AllegroSettings _allegroSettings;

        /// <summary>
        /// Zliczanie błędów Allegro wg kodu w obrębie jednego kroku. Pojedyncze błędy nadal trafiają do logu,
        /// a podsumowanie pokazuje, co faktycznie blokuje wystawianie.
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _errorCounts = new();

        /// <summary>
        /// Ile zdjęć zmieści się w galerii oferty danego produktu - ustalone po odrzuceniu "You can add up to 16 images".
        /// Bez tego każdy cykl zaczynałby od odrzuconego zapytania. Żyje tyle, co proces.
        /// </summary>
        private static readonly ConcurrentDictionary<string, int> GalleryLimits = new(StringComparer.OrdinalIgnoreCase);

        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.Preserve,
            WriteIndented = true
        };

        public AllegroOfferService(IProductRepository productRepo, IOfferRepository offerRepo, IParameterRepository parameterRepo, ICategoryRepository categoryRepo, IAllegroApiClient apiClient, IOptions<AppSettings> appsettings, IOptions<AllegroSettings> allegroSettings, IOptions<PriceSettings> priceSettings, ILogger<AllegroOfferService> logger, IImageRepository imageRepo)
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

                var shippingRates = await _apiClient.GetShippingRates(ct);
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

                    //if (offer.External?.Id != null && offer.Publication.Status != "ENDED")
                    //{
                    //    await _productRepo.UpdateProductAllegroCategory(offer.External.Id, offer.Category.Id, ct);
                    //}
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
                        var detailedOffer = await _apiClient.GetOfferDetails(offer.Id, token);

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
                var firstPage = await _apiClient.GetOffers(limit, 0, ct);

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
                        var page = await _apiClient.GetOffers(limit, offset, token);

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

                if (offers == null || !offers.Any())
                {
                    _logger.LogInformation("No offers to update.");
                    return;
                }

                _logger.LogInformation("Offers to update: {Count}.", offers.Count());

                int updated = 0;
                int failed = 0;
                int fixedData = 0;
                int skipped = 0;

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
                        // Archived products don't need fresh images - PatchOffer sends a minimal
                        // "end this offer" request for them regardless, and importing/requiring
                        // images here would just risk skipping the end-offer patch entirely.
                        if (!offer.Product.IsArchived && (offer.Product.AllegroImages == null || !offer.Product.AllegroImages.Any()))
                        {
                            var images = await ImportImages(offer.Product, token);

                            if (images == null || !images.Any())
                            {
                                Interlocked.Increment(ref skipped);
                                return;
                            }

                            offer.Product.AllegroImages = images;
                        }

                        ApplyGalleryLimit(offer.Product);

                        var offerDto = OfferFactory.PatchOffer(offer, allegroCategories, _appSettings, _allegroSettings, _priceSettings);
                        var response = await _apiClient.UpdateOffer(offer.Id, offerDto, token);
                        var body = await response.Content.ReadAsStringAsync(token);

                        if (ShrinkGalleryAfterRejection(offer.Product, response, body))
                        {
                            offerDto = OfferFactory.PatchOffer(offer, allegroCategories, _appSettings, _allegroSettings, _priceSettings);
                            response = await _apiClient.UpdateOffer(offer.Id, offerDto, token);
                            body = await response.Content.ReadAsStringAsync(token);
                        }

                        var dataFixed = await LogAllegroResponse(offer.Product, response, body, true, offer.Id);

                        if (response.IsSuccessStatusCode)
                            Interlocked.Increment(ref updated);
                        else if (dataFixed)
                            Interlocked.Increment(ref fixedData);
                        else
                            Interlocked.Increment(ref failed);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        _logger.LogError(ex, "Exception while updating offer for {Code}", offer.Product.Code);
                    }
                });

                _logger.LogInformation(
                    "Offers updated: {Updated}, failed: {Failed}, skipped (no images): {Skipped}, data fixed (retry next cycle): {Fixed}.",
                    updated, failed, skipped, fixedData);
                LogErrorSummary("Offers update");
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

                _logger.LogInformation(
                    "Products to create offers for: {Count} (min stock {MinStock}, min net price {MinPrice}).",
                    products.Count(), _appSettings.MinProductStock, _appSettings.MinProductPriceNet);

                int created = 0;
                int failed = 0;
                int fixedData = 0;
                int skipped = 0;
                int noDelivery = 0;

                await Parallel.ForEachAsync(products, new ParallelOptions
                {
                    CancellationToken = ct,
                    MaxDegreeOfParallelism = 20
                },
                async (product, token) =>
                {
                    try
                    {
                        // Bez pasującego cennika nie ma czym wysłać - Allegro i tak odrzuciłoby ofertę.
                        // Sprawdzamy przed wgraniem zdjęć, żeby nie wysyłać ich na próżno.
                        if (!OfferFactory.HasDeliveryRule(product, _appSettings))
                        {
                            Interlocked.Increment(ref noDelivery);
                            _logger.LogDebug("No delivery price list fits {Code} - offer not created.", product.Code);
                            return;
                        }

                        product.AllegroImages = await ImportImages(product, token);

                        // Allegro wymaga co najmniej jednego zdjęcia - produkt czeka na kolejny cykl.
                        if (product.AllegroImages == null || !product.AllegroImages.Any())
                        {
                            Interlocked.Increment(ref skipped);
                            return;
                        }

                        ApplyGalleryLimit(product);

                        var offer = OfferFactory.BuildOffer(product, allegroCategories, _appSettings, _allegroSettings, _priceSettings);
                        var response = await _apiClient.CreateOffer(offer, token);
                        var body = await response.Content.ReadAsStringAsync(token);

                        if (ShrinkGalleryAfterRejection(product, response, body))
                        {
                            offer = OfferFactory.BuildOffer(product, allegroCategories, _appSettings, _allegroSettings, _priceSettings);
                            response = await _apiClient.CreateOffer(offer, token);
                            body = await response.Content.ReadAsStringAsync(token);
                        }

                        var dataFixed = await LogAllegroResponse(product, response, body);

                        if (response.IsSuccessStatusCode)
                            Interlocked.Increment(ref created);
                        else if (dataFixed)
                            Interlocked.Increment(ref fixedData);
                        else
                            Interlocked.Increment(ref failed);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        _logger.LogError(ex, "Exception while creating offer for {Code}", product.Code);
                    }
                });

                _logger.LogInformation(
                    "Offers created: {Created}, failed: {Failed}, skipped (no images): {Skipped}, no matching delivery price list: {NoDelivery}, data fixed (retry next cycle): {Fixed}.",
                    created, failed, skipped, noDelivery, fixedData);
                LogErrorSummary("Offers creation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error while creating Allegro offers.");
            }
        }

        // ---------------------------------------------------------------- podsumowanie błędów

        private void CountError(string? code) =>
            _errorCounts.AddOrUpdate(string.IsNullOrWhiteSpace(code) ? "-" : code, 1, (_, current) => current + 1);

        private void LogErrorSummary(string step)
        {
            if (_errorCounts.IsEmpty)
                return;

            var summary = string.Join(", ", _errorCounts
                .OrderByDescending(x => x.Value)
                .Select(x => $"{x.Key}: {x.Value}"));

            _logger.LogWarning("{Step} - error breakdown: {Summary}", step, summary);
            _errorCounts.Clear();
        }

        // ---------------------------------------------------------------- limit galerii

        /// <summary>Przycina zdjęcia produktu do limitu zapamiętanego po wcześniejszym odrzuceniu galerii.</summary>
        private static void ApplyGalleryLimit(Product product)
        {
            if (product.AllegroImages == null || !GalleryLimits.TryGetValue(product.Code, out var limit))
                return;

            product.AllegroImages = TrimImages(product.AllegroImages, limit);
        }

        /// <summary>
        /// Odrzucenie za za dużą galerię ("GallerySizeException - You can add up to 16 images"). Zwraca limit
        /// z komunikatu albo <c>null</c>, gdy to inny błąd - w tym "przynajmniej 1 zdjęcie", czyli galeria pusta.
        /// </summary>
        internal static int? GalleryTooLargeLimit(string body)
        {
            if (string.IsNullOrEmpty(body) || !body.Contains("GallerySizeException", StringComparison.OrdinalIgnoreCase))
                return null;

            var match = Regex.Match(body, @"(?:up to|maksymalnie|do)\s+(\d+)\s+(?:images|zdję)", RegexOptions.IgnoreCase);

            if (match.Success)
                return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

            return body.Contains("przynajmniej", StringComparison.OrdinalIgnoreCase) || body.Contains("at least", StringComparison.OrdinalIgnoreCase)
                ? null
                : OfferFactory.MaxGalleryImages;
        }

        /// <summary>
        /// Ile zdjęć wysłać po odrzuceniu galerii. Allegro dolicza do galerii zdjęcia produktu z katalogu,
        /// których liczby nie znamy - zdejmujemy więc co najmniej jedno, a kolejne odrzucenie zmniejszy limit dalej.
        /// Zawsze zostaje co najmniej jedno zdjęcie.
        /// </summary>
        internal static int ReducedGallerySize(int ownImages, int limit)
        {
            var size = Math.Min(limit, ownImages - 1);
            return Math.Max(size, 1);
        }

        /// <summary>Pierwsze <paramref name="size"/> zdjęć produktu (bez powtórzeń), co najmniej jedno.</summary>
        internal static List<AllegroImages> TrimImages(List<AllegroImages> images, int size) =>
            images.GroupBy(i => i.Url).Select(g => g.First()).Take(Math.Max(size, 1)).ToList();

        /// <summary>
        /// Po odrzuceniu za za dużą galerię przycina zdjęcia produktu i zapamiętuje limit (do restartu procesu).
        /// Zwraca true, gdy jest sens od razu ponowić zapytanie z mniejszą galerią.
        /// </summary>
        private bool ShrinkGalleryAfterRejection(Product product, HttpResponseMessage response, string body)
        {
            if (response.IsSuccessStatusCode || product.AllegroImages == null)
                return false;

            var limit = GalleryTooLargeLimit(body);
            var ownImages = product.AllegroImages.Select(i => i.Url).Distinct().Count();

            if (limit == null || ownImages <= 1)
                return false;

            var size = ReducedGallerySize(ownImages, limit.Value);

            if (size >= ownImages)
                return false;

            GalleryLimits[product.Code] = size;
            product.AllegroImages = TrimImages(product.AllegroImages, size);

            _logger.LogWarning(
                "Gallery of {Code} is over the Allegro limit of {Limit} ({Own} images) - retrying with {Size} images.",
                product.Code, limit, ownImages, size);

            return true;
        }

        /// <summary>Skrócona treść odpowiedzi do logu.</summary>
        private static string Shorten(string? body, int maxLength = 1000) =>
            string.IsNullOrEmpty(body) || body.Length <= maxLength
                ? body ?? string.Empty
                : body[..maxLength] + "...";

        /// <summary>"Przedmiot, który chcesz wystawić znajduje się już na liście Twoich aktywnych ofert..."</summary>
        internal static bool IsDuplicateActiveOffer(string userMessage, string message) =>
            userMessage.Contains("już na liście Twoich aktywnych ofert", StringComparison.OrdinalIgnoreCase)
            || message.Contains("already on the list of your active offers", StringComparison.OrdinalIgnoreCase);

        /// <summary>Zwraca true, gdy błąd został naprawiony w danych produktu i ma sens kolejna próba (w następnym cyklu).</summary>
        private async Task<bool> LogAllegroResponse(Product product, HttpResponseMessage response, string body, bool isUpdate = false, string offerId = null)
        {
            var action = isUpdate ? "updated" : "created";

            switch ((int)response.StatusCode)
            {
                case 200:
                case 201:
                    _logger.LogInformation("Offer {Action} successfully for {Code}", action, product.Code);
                    await _imageRepo.MarkImagesAsConnectedAsync(product.Id, CancellationToken.None);
                    return false;

                case 202:
                    _logger.LogInformation("Offer {Action} successfully but still processing for {Code}", action, product.Code);
                    await _imageRepo.MarkImagesAsConnectedAsync(product.Id, CancellationToken.None);
                    return false;

                case 400:
                case 422:
                case 433:
                    await _imageRepo.DeleteNotConnectedImages(product.Id, CancellationToken.None);
                    return await LogAllegroErrors(product, response, body, isUpdate, offerId);

                case 401:
                    await _imageRepo.DeleteNotConnectedImages(product.Id, CancellationToken.None);
                    CountError("HTTP 401");
                    _logger.LogError("Unauthorized (401). Check token for product {Code} when {Action} offer. Response: {Body}", product.Code, action, Shorten(body));
                    return false;

                case 403:
                    await _imageRepo.DeleteNotConnectedImages(product.Id, CancellationToken.None);
                    CountError("HTTP 403");
                    _logger.LogError("Forbidden (403) while {Action} offer for {Code}. Response: {Body}", action, product.Code, Shorten(body));
                    return false;

                case 404:
                    await _imageRepo.DeleteNotConnectedImages(product.Id, CancellationToken.None);
                    CountError("HTTP 404");
                    _logger.LogWarning("Offer {OfferId} not found in Allegro ({Code}). Deleting from database.", offerId, product.Code);
                    if (!string.IsNullOrEmpty(offerId))
                        await _offerRepo.DeleteOffer(offerId, CancellationToken.None);
                    return false;

                case 429:
                    CountError("HTTP 429");
                    return false;

                default:
                    await _imageRepo.DeleteNotConnectedImages(product.Id, CancellationToken.None);
                    CountError($"HTTP {(int)response.StatusCode}");
                    _logger.LogError("Unexpected status {Status} ({StatusCode}) while {Action} offer for {Code}. Response: {Body}", (int)response.StatusCode, response.StatusCode, action, product.Code, Shorten(body));
                    return false;
            }
        }

        /// <summary>
        /// Część błędów Allegro naprawiamy w danych produktu (kategoria, parametr, zestaw kompatybilności).
        /// Zwraca true tylko wtedy, gdy naprawione zostały wszystkie błędy z odpowiedzi - przy choć jednym
        /// nienaprawionym kolejna próba z tymi samymi danymi skończy się tak samo.
        /// </summary>
        private async Task<bool> LogAllegroErrors(Product product, HttpResponseMessage response, string body, bool isUpdate = false, string offerId = null)
        {
            var action = isUpdate ? "updating" : "creating";

            AllegroErrorResponse? errorResponse = null;
            try
            {
                errorResponse = JsonSerializer.Deserialize<AllegroErrorResponse>(body, _options);
            }
            catch (JsonException)
            {
                // obsłużone niżej - zalogujemy surową treść
            }

            if (errorResponse?.Errors == null || errorResponse.Errors.Count == 0)
            {
                if (body.Contains(@"The type of this ""Compatible with"" "))
                {
                    await _productRepo.UpdateCompatibilitySet(product.Id, false, CancellationToken.None);
                    return true;
                }

                CountError($"HTTP {(int)response.StatusCode}");
                _logger.LogError("Offer {Action} error {Status} for {Code}: {Body}", action, (int)response.StatusCode, product.Code, Shorten(body));
                return false;
            }

            var fixedCount = 0;
            var unfixedCount = 0;

            foreach (var err in errorResponse.Errors)
            {
                var code = err.Code ?? string.Empty;
                var userMessage = err.UserMessage ?? string.Empty;
                var message = err.Message ?? string.Empty;
                var isFixed = false;

                try
                {
                    isFixed = await TryFixProductData(product, response, offerId, action, code, userMessage, message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fixing product data after Allegro error {ErrorCode} failed for {Code}.", code, product.Code);
                }

                if (isFixed)
                {
                    fixedCount++;
                    continue;
                }

                unfixedCount++;

                if (IsDuplicateActiveOffer(userMessage, message))
                {
                    // Oferta już istnieje na koncie, ale nie ma jej jeszcze w bazie - pojawi się po synchronizacji ofert.
                    CountError("DuplicateActiveOffer");
                    _logger.LogWarning("Offer {Action} skipped for {Code}: an active offer for this product already exists in Allegro - it will be picked up by the next offer sync.", action, product.Code);
                    continue;
                }

                CountError(string.IsNullOrEmpty(code) ? "-" : code);
                _logger.LogError(
                    "Offer {Action} error for {ProductCode} (offer {OfferId}, category {CategoryId}): Code={Code}, Message={Message}",
                    action, product.Code, offerId ?? "-", product.DefaultAllegroCategory, code, string.IsNullOrWhiteSpace(userMessage) ? message : userMessage);
            }

            if (fixedCount > 0 && unfixedCount > 0)
            {
                _logger.LogWarning(
                    "Offer {Action} for {Code}: fixed {Fixed} of {Total} errors, {Unfixed} need manual attention.",
                    action, product.Code, fixedCount, fixedCount + unfixedCount, unfixedCount);
            }

            return fixedCount > 0 && unfixedCount == 0;
        }

        /// <summary>Zwraca true, gdy błąd został naprawiony w danych produktu (albo obsłużony i nie jest błędem).</summary>
        private async Task<bool> TryFixProductData(Product product, HttpResponseMessage response, string offerId, string action, string code, string userMessage, string message)
        {
            // Zła kategoria - Allegro podaje właściwą w komunikacie.
            if ((code.Contains("ProductConstraintViolationException", StringComparison.OrdinalIgnoreCase) && userMessage.Contains("kategorii produktu", StringComparison.OrdinalIgnoreCase)
                 || code.Contains("CATEGORY_MISMATCH", StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrEmpty(userMessage))
            {
                var correctCategoryId = ExtractCorrectCategoryId(userMessage);
                if (string.IsNullOrEmpty(correctCategoryId))
                    return false;

                await _productRepo.UpdateProductAllegroCategory(product.Id, Convert.ToInt32(correctCategoryId), CancellationToken.None);
                _logger.LogInformation("Updated category for {Code} to {CategoryId}", product.Code, correctCategoryId);
                return true;
            }

            if (userMessage.Contains("nieznany producent")
                || (code == "ProductValidationException"
                    && userMessage.Contains("Tworzenie produktu z wartością", StringComparison.OrdinalIgnoreCase)
                    && userMessage.Contains("parametrze Producent części", StringComparison.OrdinalIgnoreCase)))
            {
                await _parameterRepo.UpdateParameter(product.Id, 127415, "JAG", CancellationToken.None);
                await _parameterRepo.UpdateParameter(product.Id, 247835, "JAG", CancellationToken.None);
                _logger.LogInformation("Updated parameter Producent części to 'JAG' for {Code}", product.Code);
                return true;
            }

            if ((code == "PARAMETER_MISMATCH" && !string.IsNullOrEmpty(userMessage)) ||
                (code == "ProductConstraintViolationException.DataIntegrity" &&
                 message.Contains("Incorrect value of the") &&
                 message.Contains("parameter for the offered product")))
            {
                var sourceMessage = !string.IsNullOrEmpty(userMessage) ? userMessage : message;
                var correctValue = ExtractCorrectParameterValue(sourceMessage);
                var parameterId = ExtractParameterIdFromConstraintMessage(message);

                if (string.IsNullOrEmpty(parameterId) || string.IsNullOrEmpty(correctValue))
                    return false;

                await _parameterRepo.UpdateParameter(product.Id, Convert.ToInt32(parameterId), correctValue, CancellationToken.None);
                _logger.LogInformation("Updated parameter {ParameterId} for {Code} to '{CorrectValue}'", parameterId, product.Code, correctValue);
                return true;
            }

            var errorText = !string.IsNullOrEmpty(userMessage) ? userMessage : message;

            // "Parametr zależny Jakość części (zgodnie z GVO) (214434) nie może przyjąć wartości [P - zamiennik ...],
            // gdy parametr Producent części (127415) ma wartości [Corteco]" - ustawiamy jakość oryginału.
            if (code.Contains("DependencyValidator", StringComparison.OrdinalIgnoreCase)
                && errorText.Contains($"({AllegroParametersService.GvoParameterId})", StringComparison.Ordinal))
            {
                var rejectedValue = ExtractFirstBracketValue(errorText);
                var categoryParams = await _categoryRepo.GetCategoryParametersAsync(product.DefaultAllegroCategory, CancellationToken.None);
                var gvoParam = categoryParams.FirstOrDefault(p => p.ParameterId == AllegroParametersService.GvoParameterId);
                var replacement = gvoParam == null
                    ? null
                    : AllegroParametersService.ChooseGvoReplacement(gvoParam.Values.Select(v => v.Value), rejectedValue);

                if (replacement == null)
                    return false;

                await _parameterRepo.UpdateParameter(product.Id, AllegroParametersService.GvoParameterId, replacement, CancellationToken.None);
                _logger.LogInformation("Updated GVO quality for {Code} from '{Rejected}' to '{Replacement}' (rejected for this producer).", product.Code, rejectedValue, replacement);
                return true;
            }

            // "EAN (GTIN), który zaczyna się od 201 to Numer Ograniczonej Dystrybucji - RCN i nie może być wykorzystany na Allegro"
            // - EAN wewnętrzny/sklepowy, usuwamy go z parametrów produktu.
            if (errorText.Contains("Ograniczonej Dystrybucji", StringComparison.OrdinalIgnoreCase)
                || errorText.Contains("Restricted Circulation", StringComparison.OrdinalIgnoreCase)
                || (errorText.Contains("EAN", StringComparison.OrdinalIgnoreCase) && errorText.Contains("RCN", StringComparison.Ordinal)))
            {
                await _parameterRepo.DeleteParameter("EAN (GTIN)", product.Id, CancellationToken.None);
                _logger.LogInformation("Removed restricted-circulation EAN '{Ean}' from parameters of {Code}.", product.Ean, product.Code);
                return true;
            }

            // "Nie można stworzyć produktu bez podania poprawnych wartości wszystkich parametrów wymaganych: [EAN (GTIN), Kod producenta]"
            if (code == "ProductValidationException"
                && (errorText.Contains("parametrów wymaganych", StringComparison.OrdinalIgnoreCase)
                    || errorText.Contains("required parameters", StringComparison.OrdinalIgnoreCase)))
            {
                return await FillMissingRequiredParameters(product, ExtractLastBracketValues(errorText));
            }

            if (userMessage.Contains("Podany adres obrazka jest nieprawidłowy."))
            {
                // Zdjęcia zostaną wgrane od nowa w kolejnym cyklu.
                await _imageRepo.DeleteProductImagesAsync(product.Id, CancellationToken.None);
                return true;
            }

            if (message.Contains(@"The type of this ""Compatible with"" "))
            {
                await _productRepo.UpdateCompatibilitySet(product.Id, false, CancellationToken.None);
                return true;
            }

            if (code == "ParameterNameNotFoundException" && !string.IsNullOrEmpty(userMessage))
            {
                var parameterName = ExtractParameterNameFromNotFoundMessage(userMessage);
                if (string.IsNullOrEmpty(parameterName))
                    return false;

                await _parameterRepo.DeleteParameter(parameterName, product.Id, CancellationToken.None);
                _logger.LogInformation("Deleted parameter '{ParameterName}' for product {Code} because it does not exist in category", parameterName, product.Code);
                return true;
            }

            if (code == "OfferNotFoundException" && response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Offer not found in Allegro. Deleting from database.");
                if (!string.IsNullOrEmpty(offerId))
                    await _offerRepo.DeleteOffer(offerId, CancellationToken.None);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Uzupełnia brakujące parametry wymagane (np. EAN z bazy, Kod producenta = kod towaru).
        /// Zwraca true, gdy udało się ustalić wartości wszystkich wymienionych parametrów.
        /// </summary>
        private async Task<bool> FillMissingRequiredParameters(Product product, List<string> parameterNames)
        {
            if (parameterNames.Count == 0)
                return false;

            var categoryParams = (await _categoryRepo.GetCategoryParametersAsync(product.DefaultAllegroCategory, CancellationToken.None)).ToList();
            var toInsert = new List<ProductParameter>();
            var allResolved = true;

            foreach (var name in parameterNames)
            {
                var catParam = categoryParams.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                var value = catParam == null ? null : AllegroParametersService.ResolveParameterValue(product, catParam);

                if (catParam == null || string.IsNullOrWhiteSpace(value))
                {
                    allResolved = false;
                    _logger.LogWarning("Cannot fill required parameter '{ParameterName}' for {Code} - no value in product data (EAN: '{Ean}').", name, product.Code, product.Ean);
                    continue;
                }

                if (product.Parameters?.Any(p => string.Equals(p.Name, catParam.Name, StringComparison.OrdinalIgnoreCase)) == true)
                {
                    await _parameterRepo.UpdateParameter(product.Id, catParam.ParameterId, value, CancellationToken.None);
                }
                else
                {
                    toInsert.Add(new ProductParameter
                    {
                        ProductId = product.Id,
                        CategoryParameterId = catParam.Id,
                        IsForProduct = catParam.DescribesProduct,
                        Value = value
                    });
                }

                _logger.LogInformation("Set required parameter '{ParameterName}' for {Code} to '{Value}'", catParam.Name, product.Code, value);
            }

            if (toInsert.Count > 0)
                await _parameterRepo.SaveProductParametersAsync(toInsert, CancellationToken.None);

            return allResolved;
        }

        /// <summary>Pierwsza wartość w nawiasach kwadratowych: "... wartości [P - zamiennik], gdy ..." -> "P - zamiennik".</summary>
        internal static string? ExtractFirstBracketValue(string text)
        {
            var match = Regex.Match(text ?? string.Empty, @"\[([^\]]*)\]");
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        /// <summary>Lista z ostatnich nawiasów kwadratowych: "... wymaganych: [EAN (GTIN), Kod producenta]".</summary>
        internal static List<string> ExtractLastBracketValues(string text)
        {
            var matches = Regex.Matches(text ?? string.Empty, @"\[([^\]]*)\]");
            if (matches.Count == 0)
                return new List<string>();

            return matches[^1].Groups[1].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
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

                        // Powiększone zdjęcie jest zawsze JPEG - także gdy plik był PNG.
                        var contentType = Utils.GetContentType(filePath, imageBytes, validatedBytes);

                        var uploadResult = await _apiClient.UploadImage(validatedBytes, contentType, token);

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