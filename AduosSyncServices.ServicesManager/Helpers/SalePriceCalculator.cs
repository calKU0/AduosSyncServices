using AduosSyncServices.ServicesManager.Models;

namespace AduosSyncServices.ServicesManager.Helpers
{
    public static class SalePriceCalculator
    {
        // Mirrors the products service's OfferFactory.CalculatePrice/ResolveMargin for a single unit:
        // margin comes from the range containing the net price (or the last range when none matches),
        // and the result is rounded up to a full złoty minus one grosz. With no ranges configured the
        // net price is returned unchanged.
        public static decimal Calculate(decimal priceNet, IReadOnlyList<MarginRange>? marginRanges)
        {
            if (priceNet <= 0 || marginRanges == null || marginRanges.Count == 0)
                return priceNet;

            var range = marginRanges.FirstOrDefault(r => priceNet >= r.Min && priceNet <= r.Max)
                        ?? marginRanges[^1];

            var calculated = priceNet * (1 + range.Margin / 100m);
            return Math.Ceiling(calculated) - 0.01m;
        }
    }
}
