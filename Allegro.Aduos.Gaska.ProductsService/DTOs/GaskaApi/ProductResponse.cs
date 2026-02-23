namespace Allegro.Aduos.Gaska.ProductsService.DTOs
{
    public class ProductResponse
    {
        public ApiProduct Product { get; set; }
        public int Result { get; set; }
        public string Message { get; set; }
    }

    public class ApiProduct
    {
        public int Id { get; set; }
        public string CodeGaska { get; set; }
        public string CodeCustomer { get; set; }
        public string Name { get; set; }
        public string SupplierName { get; set; }
        public string SupplierLogo { get; set; }
        public float InStock { get; set; }
        public string CurrencyPrice { get; set; }
        public decimal PriceNet { get; set; }
        public decimal PriceGross { get; set; }
        public int DeliveryType { get; set; }
        public List<ApiPackage> Packages { get; set; }
        public List<ApiCrossNumber> CrossNumbers { get; set; }
        public List<ApiComponent> Components { get; set; }
        public List<ApiRecommendedPart> RecommendedParts { get; set; }
        public List<ApiApplication> Applications { get; set; }
        public List<ApiParameter> Parameters { get; set; }
        public List<ApiImage> Images { get; set; }
        public List<ApiFile> Files { get; set; }
        public List<ApiCategory> Categories { get; set; }
    }

    public class ApiCategory
    {
        public int Id { get; set; }
        public int ParentID { get; set; }
        public string Name { get; set; }
    }

    public class ApiFile
    {
        public string Title { get; set; }
        public string Url { get; set; }
    }

    public class ApiImage
    {
        public string Title { get; set; }
        public string Url { get; set; }
    }

    public class ApiParameter
    {
        public int AttributeId { get; set; }
        public string AttributeName { get; set; }
        public string AttributeValue { get; set; }
    }

    public class ApiApplication
    {
        public int Id { get; set; }
        public int ParentID { get; set; }
        public string Name { get; set; }
    }

    public class ApiRecommendedPart
    {
        public int TwrID { get; set; }
        public string CodeGaska { get; set; }
        public float Qty { get; set; }
    }

    public class ApiComponent
    {
        public int TwrID { get; set; }
        public string CodeGaska { get; set; }
        public float Qty { get; set; }
    }

    public class ApiCrossNumber
    {
        public string CrossNumber { get; set; }
        public string CrossManufacturer { get; set; }
    }

    public class ApiPackage
    {
        public string PackUnit { get; set; }
        public float PackQty { get; set; }
        public float PackNettWeight { get; set; }
        public float PackGrossWeight { get; set; }
        public string PackEan { get; set; }
        public int PackRequired { get; set; }
    }
}