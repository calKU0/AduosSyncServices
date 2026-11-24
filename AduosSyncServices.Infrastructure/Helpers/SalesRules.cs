using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.Settings;
using System.Globalization;

namespace AduosSyncServices.Infrastructure.Helpers
{
    /// <summary>
    /// Reguły sprzedaży wspólne dla kanałów (Allegro, ShopGold): cena z narzutem, wielkość opakowania
    /// i dobór reguły dostawy. Jedna implementacja - żeby ten sam produkt miał w obu kanałach tę samą cenę,
    /// termin wysyłki i cennik dostawy wyliczone z tej samej konfiguracji.
    /// </summary>
    public static class SalesRules
    {
        /// <summary>Ilość w opakowaniu wymaganym przez Gąskę (sprzedajemy całe opakowania), minimum 1.</summary>
        public static int GetPackageQuantity(Product product)
        {
            var required = product.Packages?.FirstOrDefault(p => p.PackRequired == 1);
            var baseQty = required != null ? Convert.ToInt32(required.PackQty) : 1;

            return baseQty < 1 ? 1 : baseQty;
        }

        /// <summary>Cena brutto za opakowanie: netto Gąski * ilość * (1 + narzut), zaokrąglona do x,99.</summary>
        public static decimal CalculatePrice(decimal priceNet, int quantity, PriceSettings priceSettings)
        {
            var effectiveMargin = ResolveMargin(priceSettings, priceNet);
            var calculatedPrice = priceNet * quantity * (1 + effectiveMargin / 100m);

            return Math.Ceiling(calculatedPrice) - 0.01m;
        }

        public static decimal ResolveMargin(PriceSettings priceSettings, decimal netPrice)
        {
            var range = priceSettings.MarginRanges
                .FirstOrDefault(r => netPrice >= r.Min && netPrice <= r.Max);

            if (range == null)
                return priceSettings.MarginRanges.Last().Margin;

            return range.Margin;
        }

        /// <summary>Dostępne opakowania przy danym stanie (stan Gąski w sztukach / ilość w opakowaniu).</summary>
        public static int CalculateAvailableStock(float inStock, int quantity)
        {
            var baseAvailable = Convert.ToInt32(Math.Floor(inStock));
            var safeQuantity = Math.Max(1, quantity);
            return Math.Max(0, baseAvailable / safeQuantity);
        }

        /// <param name="requireName">
        /// Allegro bez nazwy cennika odrzuci ofertę, więc reguła bez nazwy się nie liczy. W ShopGold nazwa
        /// (lista modułów wysyłek) jest opcjonalna - reguła nadal wyznacza termin wysyłki i gabaryt.
        /// </param>
        public static DeliverySettings? GetDeliveryRule(Product product, List<DeliverySettings>? deliveries, DeliveryMatchMode matchMode, bool requireName = true)
        {
            if (deliveries == null || deliveries.Count == 0)
                return null;

            var validRules = deliveries
                .Where(d => !requireName || !string.IsNullOrWhiteSpace(d.DeliveryName))
                .ToList();

            if (!validRules.Any())
                return null;

            var productWeight = (decimal)product.Weight;
            var productLength = GetDimensionCm(product, "Długość");
            var productWidth = GetDimensionCm(product, "Szerokość");
            var productHeight = GetDimensionCm(product, "Wysokość");

            var customType2Rule = validRules
                .FirstOrDefault(r => r.RuleType == DeliveryRuleType.CustomType);

            if (product.DeliveryType == 2 && customType2Rule != null)
                return customType2Rule;

            var bulkyRule = validRules
                .Where(r => r.RuleType == DeliveryRuleType.BulkyType)
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
                .Where(r => r.RuleType == DeliveryRuleType.Standard)
                .ToList();

            var matchingRule = matchMode == DeliveryMatchMode.Price
                ? SelectByPriceThreshold(standardRules, product.PriceNet)
                : SelectByDimensionsAndWeight(standardRules, productWeight, productLength, productWidth, productHeight);

            if (matchingRule != null)
                return matchingRule;

            return validRules.FirstOrDefault();
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

        private static DeliverySettings? SelectByDimensionsAndWeight(
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

        private static DeliverySettings? SelectByPriceThreshold(IEnumerable<DeliverySettings> rules, decimal productNetPrice)
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
                .FirstOrDefault(s => string.Equals(s.Name, dimensionName, StringComparison.OrdinalIgnoreCase));

            if (spec?.Value == null)
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
    }
}
