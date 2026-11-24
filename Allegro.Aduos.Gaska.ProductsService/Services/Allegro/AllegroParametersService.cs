using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;

namespace Allegro.Aduos.Gaska.ProductsService.Services.Allegro
{
    public class AllegroParametersService : IAllegroParametersService
    {
        private readonly ILogger<AllegroParametersService> _logger;
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IParameterRepository _parameterRepo;

        public AllegroParametersService(IProductRepository productRepo, ICategoryRepository categoryRepo, IParameterRepository parameterRepository, ILogger<AllegroParametersService> logger)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _parameterRepo = parameterRepository;
            _logger = logger;
        }

        public async Task UpdateParameters(CancellationToken ct = default)
        {
            try
            {
                var products = await _productRepo.GetProductsToUpdateParameters(ct);

                // Cache category parameters
                var categoryParamsCache = new Dictionary<int, List<CategoryParameter>>();

                // Collect all parameter inserts before saving
                var allProductParameters = new List<ProductParameter>();

                foreach (var product in products)
                {
                    try
                    {
                        if (!categoryParamsCache.TryGetValue(product.DefaultAllegroCategory, out var categoryParams))
                        {
                            categoryParams = (await _categoryRepo.GetCategoryParametersAsync(product.DefaultAllegroCategory, ct)).ToList();
                            categoryParamsCache[product.DefaultAllegroCategory] = categoryParams;
                        }

                        var productParams = new List<ProductParameter>();

                        foreach (var catParam in categoryParams)
                        {
                            string value = ResolveParameterValue(product, catParam);

                            if (string.IsNullOrEmpty(value) && (catParam.Required || catParam.RequiredForProduct))
                            {
                                _logger.LogWarning("Missing required parameter {ParamName} for product {Code}", catParam.Name, product.Code);
                                continue;
                            }

                            productParams.Add(new ProductParameter
                            {
                                ProductId = product.Id,
                                CategoryParameterId = catParam.Id,
                                IsForProduct = catParam.DescribesProduct,
                                Value = value
                            });
                        }

                        if (productParams.Count > 0)
                        {
                            allProductParameters.AddRange(productParams);
                            _logger.LogInformation("Assigned {Count} parameters for product {Code}", productParams.Count, product.Code);
                        }
                    }
                    catch (Exception exProduct)
                    {
                        _logger.LogError(exProduct, "Error updating parameters for product {Code}", product.Code);
                    }
                }

                // Single bulk save at the end instead of per product
                if (allProductParameters.Any())
                {
                    await _parameterRepo.SaveProductParametersAsync(allProductParameters, ct);
                    _logger.LogInformation("Saved {Count} parameters for {ProductsCount} products", allProductParameters.Count, products.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error updating parameters for products.");
            }
        }

        /// <summary>Id parametru Allegro "Jakość części (zgodnie z GVO)".</summary>
        internal const int GvoParameterId = 214434;

        /// <summary>Domyślna jakość części - zamiennik. Allegro odrzuca ją dla części części producentów OE (np. Corteco).</summary>
        internal const string GvoReplacementValue = "P - zamiennik o jakości porównywalnej do oryginału";

        /// <summary>Wartość parametru kategorii dla produktu albo null, gdy nie umiemy jej ustalić.</summary>
        internal static string ResolveParameterValue(Product product, CategoryParameter param)
        {
            if (param == null) return null;

            var name = param.Name?.ToLowerInvariant();

            var directMappings = new Dictionary<string, Func<Product, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["stan"] = _ => "Nowy",
                ["waga produktu z opakowaniem jednostkowym"] = p => p.Weight.ToString(),
                ["numer katalogowy części"] = p => p.Code,
                ["kod producenta"] = p => p.Code,
                ["ean (gtin)"] = p => IsValidGtin(p.Ean) ? p.Ean.Trim() : null,
                ["typ maszyny"] = _ => "Inny",
                ["rodzaj skrzyni"] = _ => "Brak informacji",
                ["typ samochodu"] = _ => "Niezdefiniowany",
                ["numer katalogowy oryginału"] = p => p.Code,
                ["numery katalogowe zamienników"] = p => p.Substitutes,
                ["stan opakowania"] = _ => "oryginalne",
                ["jakość części (zgodnie z gvo)"] = _ => GvoReplacementValue
            };

            Func<Product, string> resolver;
            if (name != null && directMappings.TryGetValue(name, out resolver))
                return resolver(product);

            if (name == "producent" || name == "producent części")
                return GetMatchingValue(product, param);

            if (name == "marka" || name == "marka maszyny")
                return GetBrandMatchingValue(product, param);

            return null;
        }

        private static string GetMatchingValue(Product product, CategoryParameter param)
        {
            const string fallback = "JAG";

            if (param?.Type?.Equals("dictionary", StringComparison.OrdinalIgnoreCase) == true
                && param.Values?.Any() == true)
            {
                var dict = param.Values
                    .Where(v => !string.IsNullOrWhiteSpace(v.Value) && v.Value != "Premium")
                    .Select(v => new { Raw = v.Value, Normalized = Normalize(v.Value) })
                    .ToList();

                var dictSet = new HashSet<string>(dict.Select(d => d.Normalized));

                // 1. SupplierName exact match
                if (!string.IsNullOrEmpty(product.SupplierName))
                {
                    var supplier = Normalize(product.SupplierName);
                    var match = dict.FirstOrDefault(v => v.Normalized == supplier);
                    if (match != null) return match.Raw;
                }

                return fallback;
            }

            return !string.IsNullOrEmpty(product.SupplierName)
                ? Normalize(product.SupplierName)
                : fallback;
        }

        private static string GetBrandMatchingValue(Product product, CategoryParameter param)
        {
            const string fallback = "Inna";

            if (product.Applications == null || !product.Applications.Any())
                return fallback;

            var rootBrands = product.Applications
                .Where(a => a.ParentID == 0)
                .Select(a => a.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            if (!rootBrands.Any())
                return fallback;

            if (param?.Type?.Equals("dictionary", StringComparison.OrdinalIgnoreCase) == true && param.Values?.Any() == true)
            {
                var dictValues = param.Values.Select(v => v.Value).ToList();

                var brandMatch = dictValues.FirstOrDefault(dv => rootBrands.Any(rb => dv.Equals(rb, StringComparison.OrdinalIgnoreCase)));

                if (!string.IsNullOrEmpty(brandMatch)) return brandMatch;

                if (!string.IsNullOrEmpty(product.SupplierName))
                {
                    var supplierMatch = dictValues.FirstOrDefault(dv => dv.Equals(product.SupplierName, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(supplierMatch)) return supplierMatch;
                }

                return fallback;
            }

            return rootBrands.FirstOrDefault() ?? fallback;
        }

        /// <summary>Poprawny EAN-8 / UPC-A / EAN-13 / GTIN-14 (same cyfry + cyfra kontrolna).</summary>
        internal static bool IsValidGtin(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var code = value.Trim();
            if (code.Length is not (8 or 12 or 13 or 14) || !code.All(char.IsAsciiDigit))
                return false;

            if (code.All(c => c == '0'))
                return false;

            // Wagi 3/1 liczone od prawej, z pominięciem cyfry kontrolnej.
            var sum = 0;
            for (int i = code.Length - 2, weight = 3; i >= 0; i--, weight = weight == 3 ? 1 : 3)
                sum += (code[i] - '0') * weight;

            return (10 - sum % 10) % 10 == code[^1] - '0'
                && !IsRestrictedCirculationNumber(code);
        }

        /// <summary>
        /// Numer Ograniczonej Dystrybucji (RCN, kody wewnętrzne/sklepowe GS1) - Allegro ich nie przyjmuje.
        /// EAN-13 z prefiksem 2xx, 02x, 04x; UPC-A (12 cyfr) zaczynający się od 2 lub 4; EAN-8 od 0 lub 2.
        /// </summary>
        internal static bool IsRestrictedCirculationNumber(string code) => code.Length switch
        {
            8 => code[0] is '0' or '2',
            12 => code[0] is '2' or '4',
            13 => code[0] == '2' || code.StartsWith("02") || code.StartsWith("04"),
            _ => false
        };

        /// <summary>
        /// Wartość GVO na miejsce odrzuconej. Allegro nie pozwala na "P - zamiennik" dla części producentów
        /// dostarczających na pierwszy montaż (np. Corteco) - wybieramy wtedy jakość oryginału: najpierw
        /// "Q" (część o jakości oryginału), potem "O" (oryginał), z pominięciem odrzuconej wartości.
        /// </summary>
        internal static string? ChooseGvoReplacement(IEnumerable<string> allowedValues, string? rejectedValue)
        {
            var candidates = allowedValues
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Where(v => !string.Equals(v.Trim(), rejectedValue?.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var prefix in new[] { "Q", "O" })
            {
                var match = candidates.FirstOrDefault(v => v.TrimStart().StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase)
                                                        || v.TrimStart().StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match;
            }

            return null;
        }

        /// <summary>Czy jakość GVO oznacza zamiennik (brak parametru traktujemy jak zamiennik - to wartość domyślna).</summary>
        internal static bool IsReplacementQuality(string? gvoValue) =>
            string.IsNullOrWhiteSpace(gvoValue)
            || gvoValue.TrimStart().StartsWith("P", StringComparison.OrdinalIgnoreCase)
            || gvoValue.TrimStart().StartsWith("N", StringComparison.OrdinalIgnoreCase)
            || gvoValue.Contains("zamiennik", StringComparison.OrdinalIgnoreCase);

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var lowered = value.Trim().ToLowerInvariant();
            return lowered == "rollon-solid" ? "rollon" : lowered;
        }
    }
}