using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.Settings;
using Allegro.Aduos.Gaska.ProductsService.Settings;
using System.Globalization;
using System.Text;

namespace Allegro.Aduos.Gaska.ProductsService.Helpers
{
    public static class OfferFactory
    {
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
                publicationStatus: "ACTIVE",
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
            var quantity = GetPackageQuantity(product);

            return CreateOffer(
                product,
                quantity,
                allegroCategories,
                allegroSettings,
                appSettings,
                priceSettings,
                publicationStatus: product.InStock >= appSettings.MinProductStock && product.PriceNet >= appSettings.MinProductPriceNet ? "ACTIVE" : "ENDED",
                startingAt: offer.Status == "INACTIVE" ? DateTime.UtcNow : null,
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

            var offer = new ProductOfferRequest
            {
                Name = product.Name,
                ProductSet = BuildProductSet(product, quantity, allegroSettings),
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
                Images = product.AllegroImages.DistinctBy(i => i.Url).Select(i => i.Url).ToList(),
                Description = BuildDescription(product),
                External = new External { Id = product.Code },
                Publication = new Publication { Status = available < 1 ? "ENDED" : publicationStatus, StartingAt = startingAt },
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

        private static int GetPackageQuantity(Product product)
        {
            var baseQty = product.Packages.Any(p => p.PackRequired == 1)
                ? Convert.ToInt32(product.Packages.First(p => p.PackRequired == 1).PackQty)
                : 1;

            if (baseQty < 1)
                baseQty = 1;

            return baseQty;
        }

        private static int CalculateAvailableStock(int? stockOverride, float inStock, int quantity)
        {
            var baseAvailable = stockOverride ?? Convert.ToInt32(Math.Floor(inStock));
            var safeQuantity = Math.Max(1, quantity);
            return Math.Max(0, baseAvailable / safeQuantity);
        }

        private static List<ProductSet> BuildProductSet(
            Product product,
            int quantity,
            AllegroSettings allegroSettings,
            string fallbackCat = "319123")
        {
            var categoryId = product.DefaultAllegroCategory.ToString();
            var productObject = new ProductObject
            {
                Name = product.Name,
                Category = new Category { Id = categoryId == "0" ? fallbackCat : categoryId },
                Images = product.AllegroImages.DistinctBy(i => i.Url).Select(i => i.Url).ToList(),
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

            var multiValueParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "numery katalogowe zamienników",
                "marka",
            };

            foreach (var param in parameters.Where(p =>
                         p.IsForProduct == isForProduct &&
                         p.Name != "EAN (GTIN)" &&
                         p.Name != "Informacje o bezpieczeństwie"))
            {
                if (string.IsNullOrWhiteSpace(param.Value))
                    continue;

                var cleaned = new string(param.Value
                    .Where(ch => !char.IsControl(ch) || ch == ' ')
                    .ToArray())
                    .Trim();

                List<string> values;
                if (multiValueParams.Contains(param.Name))
                {
                    values = cleaned
                        .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
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

        private static Description BuildDescription(Product product)
        {
            var logoUrl = product.AllegroImages.Last().Url;

            var description = new Description { Sections = new List<Section>() };
            var images = product.AllegroImages
                .DistinctBy(i => i.Url)
                .Select(i => i.Url)
                .Where(url => url != logoUrl)
                .ToList();
            var imageIndex = 0;

            var nameH1Html = $"<h1>{RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(product.Name))}</h1>";
            var nameH2Html = $"<h2>{RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(product.Name))}</h2>";
            var descriptionHtml = string.IsNullOrWhiteSpace(product.Description)
                ? string.Empty
                : $"<p>➡️ {RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(product.Description))}</p>";

            var parametersHtml = string.Empty;
            if (product.Specifications?.Any() == true)
            {
                var attributesList = string.Join("",
                    product.Specifications.Select(p =>
                        $"<p>➡️ {RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(p.Name))}: {RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(p.Value))} {RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(p.UnitName))}</p>"));
                parametersHtml = $"<p><b>⚙️ Parametry/Wymiary:</b></p>{attributesList}";
            }

            var crossNumbersText = string.Empty;
            if (!string.IsNullOrEmpty(product.Substitutes))
            {
                crossNumbersText = $"<p><b>⚙️ Numery katalogowe: </b>{product.Substitutes}</p>";
            }

            var applicationsText = string.Empty;
            if (product.Applications?.Any() == true)
            {
                var applicationsByParent = product.Applications
                    .GroupBy(a => a.ParentID)
                    .ToDictionary(g => g.Key, g => g.ToList());

                if (applicationsByParent.TryGetValue(0, out var rootApps))
                {
                    string GetLeafNames(int parentId)
                    {
                        if (!applicationsByParent.ContainsKey(parentId))
                            return string.Empty;

                        var leafNames = new List<string>();
                        foreach (var child in applicationsByParent[parentId])
                        {
                            if (applicationsByParent.ContainsKey(child.ApplicationId))
                            {
                                leafNames.AddRange(GetLeafNames(child.ApplicationId)
                                    .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries));
                            }
                            else
                            {
                                leafNames.Add(RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(child.Name)));
                            }
                        }
                        return string.Join(", ", leafNames);
                    }

                    var listItems = new List<string>();
                    foreach (var rootApp in rootApps)
                    {
                        if (!applicationsByParent.ContainsKey(rootApp.ApplicationId))
                            continue;

                        foreach (var secondLevel in applicationsByParent[rootApp.ApplicationId])
                        {
                            var leafs = GetLeafNames(secondLevel.ApplicationId);
                            var li = $"<p><b>➡️ {System.Net.WebUtility.HtmlEncode(rootApp.Name)} - {System.Net.WebUtility.HtmlEncode(secondLevel.Name)}:</b></p><p> ⚙️ {leafs}</p>";
                            listItems.Add(li);
                        }
                    }

                    applicationsText = $"{string.Join("", listItems)}";
                }
            }

            var technicalDetailsText = string.Empty;
            if (!string.IsNullOrEmpty(crossNumbersText) || !string.IsNullOrEmpty(parametersHtml))
            {
                technicalDetailsText = $"<h2>☑️ DANE TECHNICZNE:</h2>";
            }

            var applicationLabel = string.Empty;
            if (!string.IsNullOrEmpty(applicationsText))
            {
                applicationLabel = $"<h2>☑️ ZASTOSOWANIE:</h2>";
            }

            description.Sections.Add(new Section { SectionItems = new List<SectionItem> { new() { Type = "TEXT", Content = nameH1Html } } });
            description.Sections.Add(new Section { SectionItems = new List<SectionItem> { new() { Type = "IMAGE", Url = logoUrl } } });

            var descriptionSection = new StringBuilder()
                .Append(nameH2Html)
                .Append("<p>✨✨✨✨</p>")
                .Append(descriptionHtml)
                .Append(technicalDetailsText)
                .Append(parametersHtml)
                .Append(crossNumbersText)
                .Append(applicationLabel)
                .Append(applicationsText)
                .Append("<h2>✨WYSOKIEJ JAKOŚCI CZĘŚĆ ZAMIENNA RENOMOWANEJ FIRMY✨</h2>");

            var descriptionSectionItems = new List<SectionItem>
            {
                new() { Type = "TEXT", Content = descriptionSection.ToString() },
                new() { Type = "IMAGE", Url = images[imageIndex++] }
            };

            description.Sections.Add(new Section { SectionItems = descriptionSectionItems });
            description.Sections.Add(new Section { SectionItems = new List<SectionItem> { new() { Type = "IMAGE", Url = logoUrl } } });

            while (imageIndex < images.Count)
            {
                var sectionImageItems = new List<SectionItem>
                {
                    new() { Type = "IMAGE", Url = images[imageIndex++] }
                };

                if (imageIndex < images.Count)
                {
                    sectionImageItems.Add(new SectionItem { Type = "IMAGE", Url = images[imageIndex++] });
                }

                // Add images section (image1,image2 etc.)
                description.Sections.Add(new Section { SectionItems = sectionImageItems });

                // Add logo section AFTER each image section
                description.Sections.Add(new Section
                {
                    SectionItems = new List<SectionItem>
                    {
                        new() { Type = "IMAGE", Url = logoUrl }
                    }
                });
            }

            return description;
        }

        private static DeliverySettings? GetDeliveryRule(Product product, List<DeliverySettings> deliveries, DeliveryMatchMode matchMode)
        {
            if (deliveries == null || deliveries.Count == 0)
                return null;

            var validRules = deliveries
                .Where(d => !string.IsNullOrWhiteSpace(d.DeliveryName))
                .ToList();

            if (!validRules.Any())
                return null;

            var productWeight = (decimal)product.Weight;
            var productLength = GetDimensionCm(product, "Długość");
            var productWidth = GetDimensionCm(product, "Szerokość");
            var productHeight = GetDimensionCm(product, "Wysokość");

            var customType2Rule = validRules
                .Where(r => IsRuleType(r, DeliveryRuleType.CustomType))
                .FirstOrDefault();

            if (product.DeliveryType == 2 && customType2Rule != null)
                return customType2Rule;

            var bulkyRule = validRules
                .Where(r => IsRuleType(r, DeliveryRuleType.BulkyType))
                .Where(r => product.DeliveryType == 1)
                .ToList();

            if (bulkyRule.Any())
            {
                var selectedBulky = matchMode == DeliveryMatchMode.Price
                    ? SelectByPriceThreshold(bulkyRule, product.PriceNet)
                    : SelectByDimensionsAndWeight(bulkyRule, productWeight, productLength, productWidth, productHeight);

                if (selectedBulky != null)
                    return selectedBulky;
            }

            var standardRules = validRules
                .Where(r => IsRuleType(r, DeliveryRuleType.Standard))
                .ToList();

            var matchingRule = matchMode == DeliveryMatchMode.Price
                ? SelectByPriceThreshold(standardRules, product.PriceNet)
                : SelectByDimensionsAndWeight(standardRules, productWeight, productLength, productWidth, productHeight);

            if (matchingRule != null)
                return matchingRule;

            return validRules
                .FirstOrDefault();
        }

        internal static string? ResolveDeliveryNameForTests(Product product, List<DeliverySettings> deliveries, DeliveryMatchMode matchMode)
        {
            return GetDeliveryRule(product, deliveries, matchMode)?.DeliveryName;
        }

        internal static string? ResolveHandlingTimeForTests(Product product, List<DeliverySettings> deliveries, DeliveryMatchMode matchMode)
        {
            return GetDeliveryRule(product, deliveries, matchMode)?.HandlingTime.ToString();
        }

        private static bool IsRuleType(DeliverySettings rule, DeliveryRuleType expectedRuleType)
        {
            return rule.RuleType == expectedRuleType;
        }

        private static bool MatchesNetThreshold(DeliverySettings rule, decimal productNetPrice)
        {
            return !rule.NetPriceThreshold.HasValue || productNetPrice >= rule.NetPriceThreshold.Value;
        }

        private static bool MatchesDimensionsAndWeight(
            DeliverySettings rule,
            decimal productWeight,
            decimal? productLength,
            decimal? productWidth,
            decimal? productHeight)
        {
            if (rule.Weight <= 0 || rule.Length <= 0 || rule.Width <= 0 || rule.Height <= 0)
                return false;

            if (productWeight > rule.Weight)
                return false;

            if (productLength.HasValue && productLength.Value > rule.Length)
                return false;

            if (productWidth.HasValue && productWidth.Value > rule.Width)
                return false;

            if (productHeight.HasValue && productHeight.Value > rule.Height)
                return false;

            return true;
        }

        private static DeliverySettings SelectByDimensionsAndWeight(
            IEnumerable<DeliverySettings> rules,
            decimal productWeight,
            decimal? productLength,
            decimal? productWidth,
            decimal? productHeight)
        {
            var list = rules.ToList();
            var matched = list
                .Where(r => MatchesDimensionsAndWeight(r, productWeight, productLength, productWidth, productHeight))
                .OrderBy(r => r.Weight)
                .ThenBy(r => r.Length * r.Width * r.Height)
                .FirstOrDefault();

            if (matched != null)
                return matched;

            return list
                .OrderByDescending(r => r.Weight)
                .ThenByDescending(r => r.Length * r.Width * r.Height)
                .FirstOrDefault();
        }

        private static DeliverySettings SelectByPriceThreshold(IEnumerable<DeliverySettings> rules, decimal productNetPrice)
        {
            var list = rules.ToList();
            var matched = list
                .Where(r => MatchesNetThreshold(r, productNetPrice))
                .OrderByDescending(r => r.NetPriceThreshold ?? 0)
                .FirstOrDefault();

            if (matched != null)
                return matched;

            return list
                .OrderBy(r => r.NetPriceThreshold ?? decimal.MaxValue)
                .FirstOrDefault();
        }

        private static decimal? GetDimensionCm(Product product, string dimensionName)
        {
            var spec = product.Specifications?
                .FirstOrDefault(s =>
                    string.Equals(s.Name, dimensionName, StringComparison.OrdinalIgnoreCase));

            if (spec == null)
                return null;

            if (!decimal.TryParse(
                    spec.Value.Replace(",", "."),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var value))
                return null;

            return spec.UnitName?.ToLower() switch
            {
                "mm" => value / 10m,
                "cm" => value,
                "m" => value * 100m,
                _ => null
            };
        }

        private static decimal CalculatePrice(Product product, PriceSettings priceSettings, int quantity)
        {
            var effectiveMargin = ResolveMargin(priceSettings, product.PriceNet);
            var calculatedPrice = product.PriceNet * quantity * (1 + effectiveMargin / 100m);

            return Math.Ceiling(calculatedPrice) - 0.01m;
        }

        private static decimal ResolveMargin(PriceSettings priceSettings, decimal netPrice)
        {
            var range = priceSettings.MarginRanges
                .FirstOrDefault(r => netPrice >= r.Min && netPrice <= r.Max);

            if (range == null)
                return priceSettings.MarginRanges.Last().Margin;

            return range.Margin;
        }

        private static string RemoveHiddenAscii(string input) =>
            string.IsNullOrEmpty(input)
                ? input
                : new string(input.Where(c => c >= 32 || c is (char)10 or (char)13).ToArray());
    }
}