using AduosSyncServices.Contracts.Data;
using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.Infrastructure.Helpers;
using Allegro.Aduos.Gaska.ProductsService.Settings;
using System.Globalization;

namespace Allegro.Aduos.Gaska.ProductsService.Helpers
{
    public static class OfferFactory
    {
        /// <summary>Maksymalna liczba zdjęć w galerii oferty Allegro.</summary>
        internal const int MaxGalleryImages = 16;

        public static ProductOfferRequest BuildOffer(
            Product product,
            List<AllegroCategory> allegroCategories,
            AppSettings appSettings,
            AllegroSettings allegroSettings,
            PriceSettings priceSettings)
        {
            var quantity = GetPackageQuantity(product);

            return CreateOffer(
                product,
                quantity,
                allegroCategories,
                allegroSettings,
                appSettings,
                priceSettings,
                publicationStatus: AllegroOfferStatuses.Active,
                startingAt: DateTime.UtcNow,
                stockOverride: null);
        }

        public static ProductOfferRequest PatchOffer(
            AllegroOffer offer,
            List<AllegroCategory> allegroCategories,
            AppSettings appSettings,
            AllegroSettings allegroSettings,
            PriceSettings priceSettings)
        {
            var product = offer.Product;

            if (product.IsArchived || product.InStock < appSettings.MinProductStock || product.PriceNet < appSettings.MinProductPriceNet)
            {
                return new ProductOfferRequest
                {
                    Publication = new Publication { Status = AllegroOfferStatuses.Ended }
                };
            }

            var quantity = GetPackageQuantity(product);

            return CreateOffer(
                product,
                quantity,
                allegroCategories,
                allegroSettings,
                appSettings,
                priceSettings,
                publicationStatus: product.InStock >= appSettings.MinProductStock && product.PriceNet >= appSettings.MinProductPriceNet ? AllegroOfferStatuses.Active : AllegroOfferStatuses.Ended,
                startingAt: offer.Status == AllegroOfferStatuses.Inactive ? DateTime.UtcNow : null,
                stockOverride: Convert.ToInt32(Math.Floor(product.InStock)));
        }

        private static ProductOfferRequest CreateOffer(
            Product product,
            int quantity,
            List<AllegroCategory> allegroCategories,
            AllegroSettings allegroSettings,
            AppSettings appSettings,
            PriceSettings priceSettings,
            string publicationStatus,
            DateTime? startingAt,
            int? stockOverride)
        {
            var price = CalculatePrice(product, priceSettings, quantity);
            var available = CalculateAvailableStock(stockOverride, product.InStock, quantity);
            var selectedDelivery = GetDeliveryRule(product, appSettings.Deliveries, appSettings.DeliveryMatchMode);
            var images = GetOfferImages(product);

            var offer = new ProductOfferRequest
            {
                Name = BuildOfferName(product),
                ProductSet = BuildProductSet(product, images, quantity, allegroSettings),
                Stock = new Stock
                {
                    Available = available,
                    Unit = MapAllegroUnit(product.Unit)
                },
                SellingMode = new SellingMode
                {
                    Format = "BUY_NOW",
                    Price = new Price
                    {
                        Amount = price.ToString("F2", CultureInfo.InvariantCulture),
                        Currency = "PLN"
                    }
                },
                Images = images,
                Description = OfferDescriptionBuilder.Build(product, images, quantity),
                External = new External { Id = product.Code },
                Publication = new Publication { Status = available < 1 ? AllegroOfferStatuses.Ended : publicationStatus, StartingAt = startingAt },
                Category = new() { Id = product.DefaultAllegroCategory.ToString() },
                Delivery = new Delivery
                {
                    ShippingRates = new ShippingRates { Name = selectedDelivery?.DeliveryName },
                    HandlingTime = selectedDelivery?.HandlingTime.ToString()
                },
                TaxSettings = new()
                {
                    Rates = new List<Rate>
                    {
                        new Rate
                        {
                            RateValue = "23.00",
                            CountryCode = "PL"
                        }
                    },
                    Subject = "GOODS"
                },
                AfterSalesServices = new AfterSalesServices
                {
                    Warranty = new Warranty { Name = allegroSettings.AllegroWarranty },
                    ReturnPolicy = new ReturnPolicy { Name = allegroSettings.AllegroReturnPolicy },
                    ImpliedWarranty = new ImpliedWarranty { Name = allegroSettings.AllegroImpliedWarranty }
                },
                Parameters = BuildParameters(product.Parameters, isForProduct: false),
                CompatibilityList = product.BuildCompatibilitySet
                    ? BuildCompatibilityList(product.DefaultAllegroCategory, product.Applications, allegroCategories)
                    : null
            };

            return offer;
        }

        /// <summary>
        /// Zdjęcia oferty (galeria + opis). Allegro przyjmuje maksymalnie 16 zdjęć, a zdjęcie użyte w opisie,
        /// którego nie ma w galerii, dokłada do galerii - więc ta sama, przycięta lista idzie wszędzie.
        /// </summary>
        /// <summary>Allegro wymaga co najmniej 3 słów w tytule oferty (ConstraintViolationException.MinWords).</summary>
        internal const int MinTitleWords = 3;

        /// <summary>
        /// Tytuł oferty: sformatowana nazwa produktu, a gdy ma mniej niż 3 słowa - dopisany kod towaru
        /// (jeśli go jeszcze nie ma w nazwie), potem "JAG". Ostatecznie "Część" na początku.
        /// </summary>
        internal static string BuildOfferName(Product product)
        {
            var name = OfferNameFormatter.Format(product.Name);
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

            foreach (var extra in new[] { product.Code, "JAG" })
            {
                if (words.Count >= MinTitleWords)
                    break;

                var word = extra?.Trim();
                if (string.IsNullOrEmpty(word) || word.Contains(' ') || words.Contains(word, StringComparer.OrdinalIgnoreCase))
                    continue;

                words.Add(word);
            }

            if (words.Count < MinTitleWords && !words.Contains("Część", StringComparer.OrdinalIgnoreCase))
                words.Insert(0, "Część");

            return string.Join(' ', words);
        }

        internal static List<string> GetOfferImages(Product product) =>
            (product.AllegroImages ?? new List<AllegroImages>())
                .Select(i => i.Url)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct()
                .Take(MaxGalleryImages)
                .ToList();

        private static int GetPackageQuantity(Product product) => SalesRules.GetPackageQuantity(product);

        private static int CalculateAvailableStock(int? stockOverride, float inStock, int quantity) =>
            SalesRules.CalculateAvailableStock(stockOverride ?? (float)Math.Floor(inStock), quantity);

        private static List<ProductSet> BuildProductSet(
            Product product,
            List<string> images,
            int quantity,
            AllegroSettings allegroSettings,
            string fallbackCat = "319123")
        {
            var categoryId = product.DefaultAllegroCategory.ToString();
            var productObject = new ProductObject
            {
                Name = BuildOfferName(product),
                Category = new Category { Id = categoryId == "0" ? fallbackCat : categoryId },
                Images = images,
                Parameters = BuildParameters(product.Parameters, isForProduct: true),
            };

            return new List<ProductSet>
            {
                new()
                {
                    ProductObject = productObject,
                    Quantity = new Quantity { Value = quantity },
                    ResponsiblePerson = new ResponsiblePerson { Name = allegroSettings.AllegroResponsiblePerson },
                    ResponsibleProducer = new ResponsibleProducer { Type = "NAME", Name = allegroSettings.AllegroResponsibleProducer },
                    SafetyInformation = new SafetyInformation { Type = "TEXT", Description = allegroSettings.AllegroSafetyMeasures },
                }
            };
        }

        private static string MapAllegroUnit(string productUnit)
        {
            if (string.IsNullOrWhiteSpace(productUnit))
                return "UNIT";

            return productUnit.Trim().ToLowerInvariant().Replace(".", "") switch
            {
                "szt" => "UNIT",
                "para" => "PAIR",
                "kpl" => "SET",
                _ => "UNIT"
            };
        }

        private static List<Parameter> BuildParameters(ICollection<ProductParameter> parameters, bool isForProduct)
        {
            var result = new List<Parameter>();

            // Separatory wartości parametrów wielowartościowych. Marka tylko po przecinku -
            // nazwy wielowyrazowe ("Massey Ferguson", "John Deere") to jedna wartość słownika.
            var multiValueParams = new Dictionary<string, char[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["numery katalogowe zamienników"] = new[] { ',', ' ' },
                ["marka"] = new[] { ',', ';' },
            };

            foreach (var param in parameters.Where(p =>
                         p.IsForProduct == isForProduct &&
                         p.Name != "Informacje o bezpieczeństwie"))
            {
                if (string.IsNullOrWhiteSpace(param.Value))
                    continue;

                var cleaned = new string(param.Value
                    .Where(ch => !char.IsControl(ch) || ch == ' ')
                    .ToArray())
                    .Trim();

                List<string> values;
                if (multiValueParams.TryGetValue(param.Name, out var separators))
                {
                    values = cleaned
                        .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(15)
                        .ToList();
                }
                else
                {
                    values = new List<string> { cleaned };
                }

                if (values.Count > 0)
                {
                    result.Add(new Parameter
                    {
                        Name = param.Name,
                        Values = values
                    });
                }
            }

            return result;
        }

        public static CompatibilityList? BuildCompatibilityList(
            int categoryId,
            IEnumerable<ProductApplication> applications,
            IEnumerable<AllegroCategory> categories)
        {
            if (applications == null || !applications.Any())
                return null;

            var categoryExists = categories.Any(c => c.Id == categoryId || c.CategoryId == categoryId.ToString());
            if (!categoryExists)
                return null;

            bool IsCategoryOrParent(int catId, string targetCategoryId)
            {
                var category = categories.FirstOrDefault(c => c.CategoryId == catId.ToString() || c.Id == catId);
                while (category != null)
                {
                    if (category.CategoryId == targetCategoryId)
                        return true;

                    if (category.ParentId == null)
                        break;

                    category = categories.FirstOrDefault(c => c.Id == category.ParentId.Value);
                }
                return false;
            }

            var leafApps = applications
                .Where(a => !applications.Any(child => child.ParentID == a.ApplicationId))
                .OrderBy(a => a.ApplicationId)
                .ToList();

            var items = new List<Item>();

            if (!IsCategoryOrParent(categoryId, "252204"))
            {
                var prohibitedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "marka" };

                foreach (var leaf in leafApps)
                {
                    var fullPath = new List<ProductApplication>();
                    var current = leaf;
                    while (current != null)
                    {
                        fullPath.Insert(0, current);
                        if (current.ParentID == 0) break;
                        current = applications.FirstOrDefault(a => a.ApplicationId == current.ParentID);
                    }

                    if (fullPath.Count == 0)
                        continue;

                    var path = new List<string> { fullPath.First().Name };
                    var leafName = fullPath.Last().Name;
                    var leafIsNumber = int.TryParse(leafName, out _);

                    if (leafIsNumber && fullPath.Count > 2)
                    {
                        var parentOfLeaf = fullPath[^2];
                        if (parentOfLeaf.ParentID != fullPath.First().ApplicationId)
                        {
                            path.Add(parentOfLeaf.Name);
                        }
                    }

                    path.Add(leafName);

                    var text = string.Join(" ", path);
                    if (prohibitedWords.Any(word => text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
                        continue;

                    if (!items.Any(i => i.Text == text))
                    {
                        items.Add(new Item { Type = "TEXT", Text = text });
                    }
                }
            }

            if (!items.Any())
                return null;

            return new CompatibilityList { Items = items.Take(99).ToList() };
        }

        private static DeliverySettings? GetDeliveryRule(Product product, List<DeliverySettings> deliveries, DeliveryMatchMode matchMode) =>
            SalesRules.GetDeliveryRule(product, deliveries, matchMode);

        /// <summary>Czy do produktu pasuje jakikolwiek cennik dostawy. Bez cennika Allegro odrzuci ofertę.</summary>
        internal static bool HasDeliveryRule(Product product, AppSettings appSettings) =>
            GetDeliveryRule(product, appSettings.Deliveries, appSettings.DeliveryMatchMode) != null;

        private static decimal CalculatePrice(Product product, PriceSettings priceSettings, int quantity) =>
            SalesRules.CalculatePrice(product.PriceNet, quantity, priceSettings);
    }
}