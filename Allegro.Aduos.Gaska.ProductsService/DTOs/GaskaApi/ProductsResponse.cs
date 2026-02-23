namespace Allegro.Aduos.Gaska.ProductsService.DTOs
{
    public class ProductsResponse
    {
        public List<ApiProducts> Products { get; set; }
        public int Result { get; set; }
        public string Message { get; set; }
    }

    public class ApiProducts
    {
        public int Id { get; set; }
        public string CodeGaska { get; set; }
        public string CodeCustomer { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public string Ean { get; set; }
        public string SupplierName { get; set; }
        public string SupplierLogo { get; set; }
        public string Description { get; set; }
        public string TechnicalDetails { get; set; }
        public decimal NetPrice { get; set; }
        public decimal GrossPrice { get; set; }
        public string CurrencyPrice { get; set; }
        public int DeliveryType { get; set; }
        public float NetWeight { get; set; }
        public float GrossWeight { get; set; }
        public float InStock { get; set; }
    }
}