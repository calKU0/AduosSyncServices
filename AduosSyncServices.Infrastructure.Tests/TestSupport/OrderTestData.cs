using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Models;

namespace AduosSyncServices.Infrastructure.Tests.TestSupport
{
    // Small builders so the placement/stock tests read as "an order for product X, qty N" instead of
    // pages of property initialisers.
    internal static class OrderTestData
    {
        public static Product Product(int id, int integrationId, string code, string name = "Produkt", string unit = "szt.", int deliveryType = 0)
            => new()
            {
                Id = id,
                IntegrationId = integrationId,
                Code = code,
                Name = name,
                Unit = unit,
                DeliveryType = deliveryType
            };

        public static AllegroOrder Order(
            int id,
            string allegroId,
            (int productId, int quantity)[] items,
            AllegroPaymentType paymentType = AllegroPaymentType.ONLINE,
            bool sentToExternalCompany = false,
            OrderSource source = OrderSource.Allegro,
            string recipientFirstName = "Jan",
            string recipientLastName = "Kowalski",
            string? recipientCompanyName = null,
            decimal amount = 100m)
            => new()
            {
                Id = id,
                AllegroId = allegroId,
                PaymentType = paymentType,
                SentToExternalCompany = sentToExternalCompany,
                Source = source,
                RecipientFirstName = recipientFirstName,
                RecipientLastName = recipientLastName,
                RecipientCompanyName = recipientCompanyName,
                RecipientStreet = "ul. Testowa 1",
                RecipientCity = "Warszawa",
                RecipientPostalCode = "00-001",
                RecipientCountry = "PL",
                RecipientPhoneNumber = "500600700",
                RecipientEmail = "buyer@example.com",
                Amount = amount,
                Items = items.Select((it, idx) => new AllegroOrderItem
                {
                    Id = id * 100 + idx,
                    AllegroOrderId = id,
                    ProductId = it.productId,
                    OrderItemId = $"{allegroId}-item-{idx}",
                    Quantity = it.quantity,
                    PriceGross = "10.00"
                }).ToList()
            };
    }
}
