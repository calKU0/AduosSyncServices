using AduosSyncServices.ServicesManager.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AduosSyncServices.ServicesManager.Validation
{
    public static class MarginRangeValidator
    {
        public static (List<MarginRange> Ranges, List<string> Errors) Validate(IEnumerable<(string Min, string Max, string Margin)> inputs)
        {
            var ranges = new List<MarginRange>();
            var errors = new List<string>();

            foreach (var input in inputs)
            {
                if (!TryParseDecimal(input.Min, out var min) ||
                    !TryParseDecimal(input.Max, out var max) ||
                    !TryParseDecimal(input.Margin, out var margin))
                {
                    errors.Add(ValidationMessages.MarginInvalidNumbers);
                    continue;
                }

                if (min < 0 || max < 0)
                    errors.Add(ValidationMessages.MarginNegative);

                if (min > max)
                    errors.Add(ValidationMessages.MarginMinGreaterThanMax);

                ranges.Add(new MarginRange
                {
                    Min = min,
                    Max = max,
                    Margin = margin
                });
            }

            if (!ranges.Any())
            {
                errors.Add(ValidationMessages.MarginMissingRange);
                return (ranges, errors.Distinct().ToList());
            }

            var orderedRanges = ranges.OrderBy(r => r.Min).ToList();

            if (orderedRanges[0].Min != 0m)
                errors.Add(ValidationMessages.MarginMustStartAtZero);

            for (int i = 0; i < orderedRanges.Count - 1; i++)
            {
                if (orderedRanges[i + 1].Min <= orderedRanges[i].Max)
                    errors.Add(ValidationMessages.MarginOverlap);
            }

            return (orderedRanges, errors.Distinct().ToList());
        }

        private static bool TryParseDecimal(string input, out decimal result)
        {
            var normalized = input.Replace(',', '.');
            return decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out result);
        }
    }
}
