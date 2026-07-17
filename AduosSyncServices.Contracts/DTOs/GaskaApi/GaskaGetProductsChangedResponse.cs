namespace AduosSyncServices.Contracts.DTOs.GaskaApi
{
    public class GaskaGetProductsChangedResponse
    {
        public List<ProductChanged> Products { get; set; }
    }
    public class ProductChanged
    {
        public int TwrId { get; set; }
        public string CodeGaska { get; set; }
    }
}
