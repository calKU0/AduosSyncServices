using AduosSyncServices.Contracts.Data.Enums;

namespace AduosSyncServices.Contracts.Models
{
    public class AllegroOrder
    {
        public int Id { get; set; }
        public string AllegroId { get; set; } = string.Empty;
        public string? MessageToSeller { get; set; }
        public string? Note { get; set; }
        public AllegroCheckoutFormStatus Status { get; set; }
        public AllegroOrderStatus RealizeStatus { get; set; }
        public decimal Amount { get; set; }
        public string ClientNickname { get; set; } = string.Empty;
        public string RecipientFirstName { get; set; } = string.Empty;
        public string RecipientLastName { get; set; } = string.Empty;
        public string RecipientStreet { get; set; } = string.Empty;
        public string RecipientCity { get; set; } = string.Empty;
        public string RecipientPostalCode { get; set; } = string.Empty;
        public string RecipientCountry { get; set; } = string.Empty;
        public string? RecipientCompanyName { get; set; }
        public string? RecipientEmail { get; set; } = string.Empty;
        public string? RecipientPhoneNumber { get; set; }
        public string DeliveryMethodId { get; set; } = string.Empty;
        public string DeliveryMethodName { get; set; } = string.Empty;
        public DateTime? CancellationDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Revision { get; set; } = string.Empty;
        public AllegroPaymentType PaymentType { get; set; }
        public bool SentToExternalCompany { get; set; }
        public int ExternalOrderId { get; set; }
        public string? ExternalOrderStatus { get; set; }
        public string? ExternalOrderNumber { get; set; }
        public string? ExternalDeliveryName { get; set; }
        public bool EmailSent { get; set; }
        public AllegroAccount Account { get; set; }
        public IntegrationCompany IntegrationCompany { get; set; }
        public List<AllegroOrderItem> Items { get; set; } = new();
    }
}