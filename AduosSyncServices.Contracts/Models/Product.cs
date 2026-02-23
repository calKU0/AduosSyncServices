using AduosSyncServices.Contracts.Data.Enums;

namespace AduosSyncServices.Contracts.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string? AllegroId { get; set; }
        public string Code { get; set; }
        public string? CustomerCode { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? Ean { get; set; }
        public float Weight { get; set; }
        public string? Fits { get; set; }
        public string? SupplierName { get; set; }
        public string? SupplierLogo { get; set; }
        public string? Substitutes { get; set; }
        public float InStock { get; set; }
        public string Unit { get; set; }
        public string? CurrencyPrice { get; set; }
        public decimal PriceNet { get; set; }
        public decimal PriceGross { get; set; }
        public int DefaultAllegroCategory { get; set; }
        public decimal Package { get; set; }
        public bool BuildCompatibilitySet { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
        public IntegrationCompany IntegrationCompany { get; set; }
        public int IntegrationId { get; set; }
        public int DeliveryType { get; set; }
        public List<ProductPackage>? Packages { get; set; }
        public List<ProductApplication>? Applications { get; set; }
        public List<ProductSpecification>? Specifications { get; set; }
        public List<ProductParameter>? Parameters { get; set; }
        public List<ProductCategory>? Categories { get; set; }
        public List<AllegroImages>? AllegroImages { get; set; }
    }
}