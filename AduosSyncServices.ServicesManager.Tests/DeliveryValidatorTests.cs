using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.ServicesManager.Validation;
using Xunit;

namespace AduosSyncServices.ServicesManager.Tests
{
    public class DeliveryValidatorTests
    {
        // (RuleType, HandlingTime, NetPriceThreshold, Weight, Length, Width, Height, Name)
        private static (string, string, string, string, string, string, string, string) PriceRule(string ruleType, string threshold, string name)
            => (ruleType, "PT24H", threshold, "", "", "", "", name);

        private static IEnumerable<(string, string, string, string, string, string, string, string)> ValidPriceSet() => new[]
        {
            PriceRule("Standard", "0", "STD"),
            PriceRule("BulkyType", "0", "BULKY"),
            PriceRule("CustomType", "0", "CUSTOM")
        };

        [Fact]
        public void Validate_ValidPriceModeSet_NoErrors()
        {
            var (deliveries, errors) = DeliveryValidator.Validate(DeliveryMatchMode.Price, ValidPriceSet());

            Assert.Empty(errors);
            Assert.Equal(3, deliveries.Count);
        }

        [Fact]
        public void Validate_MissingBulkyRule_ReportsMissingBulky()
        {
            var inputs = new[] { PriceRule("Standard", "0", "STD"), PriceRule("CustomType", "0", "CUSTOM") };
            var (_, errors) = DeliveryValidator.Validate(DeliveryMatchMode.Price, inputs);
            Assert.Contains(ValidationMessages.DeliveryMissingBulkyRule, errors);
        }

        [Fact]
        public void Validate_PriceModeWithWeight_ReportsNoWeightInPriceMode()
        {
            var inputs = new[] { ("Standard", "PT24H", "0", "5", "", "", "", "STD") };
            var (_, errors) = DeliveryValidator.Validate(DeliveryMatchMode.Price, inputs);
            Assert.Contains(ValidationMessages.DeliveryNoWeightInPriceMode, errors);
        }

        [Fact]
        public void Validate_InvalidRuleType_ReportsInvalidRuleType()
        {
            var inputs = new[] { ("Nonsense", "PT24H", "0", "", "", "", "", "X") };
            var (_, errors) = DeliveryValidator.Validate(DeliveryMatchMode.Price, inputs);
            Assert.Contains(ValidationMessages.DeliveryInvalidRuleType, errors);
        }

        [Fact]
        public void Validate_MissingName_ReportsMissingName()
        {
            var inputs = new[]
            {
                PriceRule("Standard", "0", ""),
                PriceRule("BulkyType", "0", "BULKY"),
                PriceRule("CustomType", "0", "CUSTOM")
            };
            var (_, errors) = DeliveryValidator.Validate(DeliveryMatchMode.Price, inputs);
            Assert.Contains(ValidationMessages.DeliveryMissingName, errors);
        }

        [Fact]
        public void Validate_PriceModeWithoutZeroThreshold_ReportsMustStartAtZero()
        {
            var inputs = new[]
            {
                PriceRule("Standard", "100", "STD"),
                PriceRule("BulkyType", "0", "BULKY"),
                PriceRule("CustomType", "0", "CUSTOM")
            };
            var (_, errors) = DeliveryValidator.Validate(DeliveryMatchMode.Price, inputs);
            Assert.Contains(ValidationMessages.DeliveryPriceModeMustStartAtZero, errors);
        }
    }
}
