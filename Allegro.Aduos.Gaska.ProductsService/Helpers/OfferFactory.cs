using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.Infrastructure.Helpers;
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
                categoryId: product.DefaultAllegroCategory.ToString(),
                name: product.Name,
                stockOverride: null,
                includeCategory: true,
                includeProductParameters: true);
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
                startingAt: null,
                categoryId: null,          // nie nadpisujemy kategorii przy patchu
                name: null,                // nie nadpisujemy nazwy przy patchu
                stockOverride: Convert.ToInt32(Math.Floor(product.InStock)),
                includeCategory: false,
                includeProductParameters: false);
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
            string? categoryId,
            string? name,
            int? stockOverride,
            bool includeCategory,
            bool includeProductParameters)
        {
            var price = CalculatePrice(product, priceSettings, quantity);
            var available = CalculateAvailableStock(stockOverride, product.InStock, quantity);

            var offer = new ProductOfferRequest
            {
                ProductSet = BuildProductSet(product, quantity, allegroSettings, includeProductParameters),
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
                Delivery = new Delivery
                {
                    ShippingRates = new ShippingRates { Name = GetDelivery(product, appSettings.Deliveries) },
                    HandlingTime = product.DeliveryType == 0
                        ? allegroSettings.AllegroHandlingTime
                        : allegroSettings.AllegroHandlingTimeCustomProducts
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

            if (includeCategory && categoryId is not null)
            {
                offer.Category = new Category { Id = categoryId };
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                offer.Name = name;
            }

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
            bool includeProductParameters = true,
            string fallbackCat = "319123")
        {
            var categoryId = product.DefaultAllegroCategory.ToString();
            var productObject = new ProductObject
            {
                Name = product.Name,
                Category = new Category { Id = categoryId == "0" ? fallbackCat : categoryId },
                Images = product.AllegroImages.DistinctBy(i => i.Url).Select(i => i.Url).ToList(),
                Parameters = includeProductParameters ? BuildParameters(product.Parameters, isForProduct: true) : null,
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
            var description = new Description { Sections = new List<Section>() };
            var images = product.AllegroImages.DistinctBy(i => i.Url).Select(i => i.Url).ToList();
            var imageIndex = 0;

            if (images.Any())
            {
                description.Sections.Add(new Section
                {
                    SectionItems = new List<SectionItem>
                    {
                        new() { Type = "IMAGE", Url = images[imageIndex++] }
                    }
                });
            }

            var originalHtml = string.IsNullOrEmpty(product.SupplierName)
                ? $"<h2>{RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode("PRODUKT JEST ZAMIENNIKIEM"))}</h2>"
                : string.Empty;

            var nameHtml = $"<p><b>{RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(product.Name))}</b></p>";
            var codeHtml = string.IsNullOrWhiteSpace(product.Code)
                ? string.Empty
                : $"<p><b>Kod produktu: </b>{RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(product.Code))}</p>";
            var producerHtml = string.IsNullOrWhiteSpace(product.SupplierName)
                ? string.Empty
                : $"<p><b>Producent: </b>{RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(product.SupplierName))}</p>";
            var descriptionHtml = string.IsNullOrWhiteSpace(product.Description)
                ? string.Empty
                : $"<p><b>Opis: </b>{RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(product.Description))}</p>";

            var parametersHtml = string.Empty;
            if (product.Specifications?.Any() == true)
            {
                var attributesList = string.Join("",
                    product.Specifications.Select(p =>
                        $"<li>{RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(p.Name))}: {RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(p.Value))} {RemoveHiddenAscii(System.Net.WebUtility.HtmlEncode(p.UnitName))}</li>"));
                parametersHtml = $"<p><b>Parametry/Wymiary:</b></p><ul>{attributesList}</ul>";
            }

            var package = product.Packages?.FirstOrDefault(p => p.PackRequired == 1);
            var warning = string.Empty;

            if (string.Equals(product.Unit, "MB", StringComparison.OrdinalIgnoreCase))
            {
                warning = $"<p><b>UWAGA:</b> {System.Net.WebUtility.HtmlEncode("PODANA CENA KUP TERAZ TO CENA ZA 1 METR BIEŻĄCY")}</p>";
            }

            if (package != null)
            {
                warning = $"<p><b>UWAGA:</b> {System.Net.WebUtility.HtmlEncode($"PODANA CENA KUP TERAZ TO CENA ZA 1 KOMPLET = {package.PackQty} {ConjugationHelper.Unit(Convert.ToInt32(package.PackQty), product.Unit).ToUpper()}")}</p>";
            }

            var crossNumbersText = string.Empty;
            if (!string.IsNullOrEmpty(product.Substitutes))
            {
                crossNumbersText = $"<p><b>Numery referencyjne: </b>{product.Substitutes}</p>";
            }

            var contentBuilder = new StringBuilder()
                .Append(originalHtml)
                .Append(nameHtml)
                .Append(codeHtml)
                .Append(producerHtml)
                .Append(descriptionHtml)
                .Append(parametersHtml)
                .Append(crossNumbersText)
                .Append(warning);

            var sectionItems = new List<SectionItem>
            {
                new() { Type = "TEXT", Content = contentBuilder.ToString() }
            };

            if (imageIndex < images.Count - 1)
            {
                sectionItems.Add(new SectionItem { Type = "IMAGE", Url = images[imageIndex++] });
            }

            description.Sections.Add(new Section { SectionItems = sectionItems });

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
                            var li = $"<li><b>{System.Net.WebUtility.HtmlEncode(rootApp.Name)} - {System.Net.WebUtility.HtmlEncode(secondLevel.Name)}</b>: {leafs}</li>";
                            listItems.Add(li);
                        }
                    }

                    var appsText = $"<ul>{string.Join("", listItems)}</ul>";

                    var appSectionItems = new List<SectionItem>();
                    if (imageIndex < images.Count - 1)
                    {
                        appSectionItems.Add(new SectionItem { Type = "IMAGE", Url = images[imageIndex++] });
                    }

                    appSectionItems.Add(new SectionItem
                    {
                        Type = "TEXT",
                        Content = $"<p><b>Zastosowanie: </b></p>{appsText}"
                    });

                    description.Sections.Add(new Section { SectionItems = appSectionItems });
                }
            }

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

                description.Sections.Add(new Section { SectionItems = sectionImageItems });
            }

            return description;
        }

        private static string GetDelivery(Product product, List<DeliverySettings> deliveries)
        {
            if (deliveries == null || deliveries.Count == 0)
                return null;

            var productWeight = (decimal)product.Weight;

            var length = GetDimensionCm(product, "Długość");
            var width = GetDimensionCm(product, "Szerokość");
            var height = GetDimensionCm(product, "Wysokość");

            var matchingDelivery = deliveries
                .Where(d =>
                    d.Weight >= productWeight &&
                    (length == null || d.Length >= length) &&
                    (width == null || d.Width >= width) &&
                    (height == null || d.Height >= height))
                .OrderBy(d => d.Weight)
                .ThenBy(d => d.Length * d.Width * d.Height) // smallest volume wins
                .FirstOrDefault();

            // Fallback: biggest delivery
            return matchingDelivery?.DeliveryName
                ?? deliveries
                    .OrderByDescending(d => d.Weight)
                    .ThenByDescending(d => d.Length * d.Width * d.Height)
                    .First()
                    .DeliveryName;
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
            var calculatedPrice = product.PriceGross;

            var effectiveMargin = ResolveMargin(priceSettings, product.PriceGross);
            calculatedPrice = product.PriceGross * quantity * (1 + effectiveMargin / 100m);

            return calculatedPrice;
        }

        private static decimal ResolveMargin(PriceSettings priceSettings, decimal grossPrice)
        {
            var range = priceSettings.MarginRanges
                .FirstOrDefault(r => grossPrice >= r.Min && grossPrice <= r.Max);

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