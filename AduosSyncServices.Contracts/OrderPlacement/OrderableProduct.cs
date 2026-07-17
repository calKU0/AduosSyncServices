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
        public decimal SalePrice { get; set; }
        public string? ImageUrl { get; set; }
        public float PackQty { get; set; } = 1;
    }
}
