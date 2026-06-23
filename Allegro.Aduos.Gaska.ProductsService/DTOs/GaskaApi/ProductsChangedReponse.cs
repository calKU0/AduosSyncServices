namespace Allegro.Aduos.Gaska.ProductsService.DTOs.GaskaApi
{
    public class ProductsChangedReponse
    {
        public List<ProductChanged> Products { get; set; }
    }
    public class ProductChanged
    {
        public int TwrId { get; set; }
        public string CodeGaska { get; set; }
    }
}
