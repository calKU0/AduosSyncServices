namespace AduosSyncServices.Contracts.OrderPlacement
{
    public class OrderableProduct
    {
        public int ProductId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public float InStock { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal PurchasePriceNet { get; set; }
        public decimal PurchasePriceGross { get; set; }
        public int DeliveryType { get; set; }

        // Not returned by the search query - computed by the UI as net price x the products-service
        // margin (see SalePriceCalculator).
        public decimal SalePrice { get; set; }

        // Not returned by the search query - resolved by the UI as the product's first image file
        // from the products-service image folder.
        public string? ImageUrl { get; set; }
        public float PackQty { get; set; } = 1;
    }
}
