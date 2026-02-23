namespace AduosSyncServices.Contracts.Models
{
    public class AllegroOrderItem
    {
        public int Id { get; set; }
        public int AllegroOrderId { get; set; }
        public int ProductId { get; set; }
        public string OrderItemId { get; set; } = string.Empty;
        public string OfferId { get; set; } = string.Empty;
        public string OfferName { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty;
        public string PriceGross { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? ExternalCourier { get; set; }
        public string? ExternalTrackingNumber { get; set; }
        public string? ShippingRate { get; set; }
        public DateTime BoughtAt { get; set; }
    }
}