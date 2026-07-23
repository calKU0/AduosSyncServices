using AduosSyncServices.ServicesManager.Validation;
using Xunit;

namespace AduosSyncServices.ServicesManager.Tests
{
    public class MarginRangeValidatorTests
    {
        [Fact]
        public void Validate_ValidContiguousRangesFromZero_NoErrors()
        {
            var (ranges, errors) = MarginRangeValidator.Validate(new[]
            {
                ("0", "50", "20"),
                ("50.01", "100", "10"),
                ("100.01", "999999", "5")
            });

            Assert.Empty(errors);
            Assert.Equal(3, ranges.Count);
            Assert.Equal(0m, ranges[0].Min); // returned sorted by Min
        }

        [Fact]
        public void Validate_AcceptsCommaDecimalSeparator()
        {
            var (_, errors) = MarginRangeValidator.Validate(new[] { ("0", "50,5", "20") });
            Assert.DoesNotContain(ValidationMessages.MarginInvalidNumbers, errors);
        }

        [Fact]
        public void Validate_NonNumeric_ReportsInvalidNumbers()
        {
            var (_, errors) = MarginRangeValidator.Validate(new[] { ("0", "abc", "20") });
            Assert.Contains(ValidationMessages.MarginInvalidNumbers, errors);
        }

        [Fact]
        public void Validate_FirstRangeNotStartingAtZero_ReportsMustStartAtZero()
        {
            var (_, errors) = MarginRangeValidator.Validate(new[] { ("10", "50", "20") });
            Assert.Contains(ValidationMessages.MarginMustStartAtZero, errors);
        }

        [Fact]
        public void Validate_OverlappingRanges_ReportsOverlap()
        {
            var (_, errors) = MarginRangeValidator.Validate(new[]
            {
                ("0", "60", "20"),
                ("50", "100", "10")
            });
            Assert.Contains(ValidationMessages.MarginOverlap, errors);
        }

        [Fact]
        public void Validate_MinGreaterThanMax_ReportsError()
        {
            var (_, errors) = MarginRangeValidator.Validate(new[] { ("100", "50", "20") });
            Assert.Contains(ValidationMessages.MarginMinGreaterThanMax, errors);
        }

        [Fact]
        public void Validate_NoRanges_ReportsMissingRange()
        {
            var (_, errors) = MarginRangeValidator.Validate(Array.Empty<(string, string, string)>());
            Assert.Contains(ValidationMessages.MarginMissingRange, errors);
        }
    }
}
