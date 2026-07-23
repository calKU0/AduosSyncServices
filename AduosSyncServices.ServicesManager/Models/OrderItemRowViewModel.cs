using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Extensions;
using System.Globalization;

namespace AduosSyncServices.ServicesManager.Models
{
    // A single line item shown in an order's expandable sub-list (and reused for building the
    // details/placement item rows): static display data resolved once when orders are loaded.
    public class OrderItemRowViewModel
    {
        public string? ImageUrl { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string QuantityDisplay { get; init; } = "-";
        public string Unit { get; init; } = "-";
        public string DeliveryTypeDisplay { get; init; } = "-";
        public string PriceDisplay { get; init; } = "-";

        public static string FormatDeliveryType(int? deliveryType) =>
            deliveryType is { } dt && Enum.IsDefined(typeof(ProductDeliveryType), dt)
                ? ((ProductDeliveryType)dt).GetDescription()
                : "-";

        // Order items store the sale price as an invariant string (e.g. "24.99") with the currency
        // held separately; format it for display, falling back to the raw value if unparseable.
        public static string FormatPrice(string priceGross)
        {
            if (decimal.TryParse(priceGross, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
                return price.ToString("N2", CultureInfo.GetCultureInfo("pl-PL")) + " zł";

            return string.IsNullOrWhiteSpace(priceGross) ? "-" : priceGross;
        }
    }
}
