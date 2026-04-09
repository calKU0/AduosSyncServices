using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.ServicesManager.Models;
using System.Globalization;

namespace AduosSyncServices.ServicesManager.Validation
{
    public static class DeliveryValidator
    {
        public static (List<Delivery> Deliveries, List<string> Errors) Validate(
            DeliveryMatchMode matchMode,
            IEnumerable<(string RuleType, string NetPriceThreshold, string Weight, string Length, string Width, string Height, string Name)> inputs)
        {
            var deliveries = new List<Delivery>();
            var errors = new List<string>();

            foreach (var input in inputs)
            {
                if (!TryParseRuleType(input.RuleType, out var ruleType))
                {
                    errors.Add(ValidationMessages.DeliveryInvalidRuleType);
                    continue;
                }

                if (!TryParseNullableDecimal(input.NetPriceThreshold, out var netPriceThreshold))
                {
                    errors.Add(ValidationMessages.DeliveryInvalidNumbers);
                    continue;
                }

                if (netPriceThreshold.HasValue && netPriceThreshold < 0)
                    errors.Add(ValidationMessages.DeliveryNetPriceThresholdNonPositive);

                if (!TryParseNullableDecimal(input.Weight, out var weight))
                {
                    errors.Add(ValidationMessages.DeliveryInvalidNumbers);
                    continue;
                }

                if (!TryParseNullableInt(input.Length, out var length) ||
                    !TryParseNullableInt(input.Width, out var width) ||
                    !TryParseNullableInt(input.Height, out var height))
                {
                    errors.Add(ValidationMessages.DeliveryInvalidNumbers);
                    continue;
                }

                if (matchMode == DeliveryMatchMode.Weight)
                {
                    if (netPriceThreshold.HasValue)
                        errors.Add(ValidationMessages.DeliveryNoThresholdInWeightMode);

                    if (!weight.HasValue || weight.Value <= 0)
                        errors.Add(ValidationMessages.DeliveryWeightRuleRequiresWeight);

                    if (!length.HasValue || !width.HasValue || !height.HasValue || length.Value <= 0 || width.Value <= 0 || height.Value <= 0)
                        errors.Add(ValidationMessages.DeliveryDimensionsRequiredInWeightMode);
                }

                if (matchMode == DeliveryMatchMode.Price)
                {
                    if (weight.HasValue)
                        errors.Add(ValidationMessages.DeliveryNoWeightInPriceMode);

                    if (length.HasValue || width.HasValue || height.HasValue)
                        errors.Add(ValidationMessages.DeliveryNoDimensionsInPriceMode);

                    if (!netPriceThreshold.HasValue)
                        errors.Add(ValidationMessages.DeliveryMissingNetPriceThreshold);
                }

                if (string.IsNullOrWhiteSpace(input.Name?.Trim()))
                    errors.Add(ValidationMessages.DeliveryMissingName);

                deliveries.Add(new Delivery
                {
                    RuleType = ruleType,
                    NetPriceThreshold = netPriceThreshold,
                    Length = length ?? 0,
                    Width = width ?? 0,
                    Height = height ?? 0,
                    Weight = weight ?? 0,
                    DeliveryName = input.Name?.Trim() ?? string.Empty
                });
            }

            ValidateRuleConsistency(matchMode, deliveries, errors);

            return (deliveries, errors.Distinct().ToList());
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

        private static bool TryParseNullableDecimal(string input, out decimal? result)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                result = null;
                return true;
            }

            if (TryParseDecimal(input, out var parsed))
            {
                result = parsed;
                return true;
            }

            result = null;
            return false;
        }

        private static bool TryParseNullableInt(string input, out int? result)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                result = null;
                return true;
            }

            if (int.TryParse(input, out var parsed))
            {
                result = parsed;
                return true;
            }

            result = null;
            return false;
        }

        private static bool TryParseRuleType(string input, out DeliveryRuleType ruleType)
        {
            return System.Enum.TryParse(input, ignoreCase: true, out ruleType);
        }

        private static void ValidateRuleConsistency(DeliveryMatchMode matchMode, List<Delivery> deliveries, List<string> errors)
        {
            if (!deliveries.Any())
            {
                errors.Add(ValidationMessages.DeliveryMissingAnyRule);
                return;
            }

            if (!deliveries.Any(d => d.RuleType == DeliveryRuleType.Standard))
                errors.Add(ValidationMessages.DeliveryMissingStandardRule);

            if (!deliveries.Any(d => d.RuleType == DeliveryRuleType.BulkyType))
                errors.Add(ValidationMessages.DeliveryMissingBulkyRule);

            if (!deliveries.Any(d => d.RuleType == DeliveryRuleType.CustomType))
                errors.Add(ValidationMessages.DeliveryMissingCustomTypeRule);

            if (matchMode == DeliveryMatchMode.Price)
            {
                var priceRuleTypes = new[] { DeliveryRuleType.Standard, DeliveryRuleType.BulkyType, DeliveryRuleType.CustomType };
                foreach (var type in priceRuleTypes)
                {
                    var hasZeroThreshold = deliveries
                        .Where(d => d.RuleType == type)
                        .Any(d => d.NetPriceThreshold.HasValue && d.NetPriceThreshold.Value == 0m);

                    if (!hasZeroThreshold)
                        errors.Add(ValidationMessages.DeliveryPriceModeMustStartAtZero);
                }
            }

            if (matchMode == DeliveryMatchMode.Weight)
            {
                var weightRuleTypes = new[] { DeliveryRuleType.Standard, DeliveryRuleType.BulkyType, DeliveryRuleType.CustomType };
                foreach (var type in weightRuleTypes)
                {
                    var hasFallback = deliveries
                        .Where(d => d.RuleType == type)
                        .Any(d => d.Weight >= 9999m && d.Length >= 9999 && d.Width >= 9999 && d.Height >= 9999);

                    if (!hasFallback)
                        errors.Add(ValidationMessages.DeliveryWeightModeRequiresFallbackRule);
                }
            }
        }
    }
}
