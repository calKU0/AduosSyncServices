namespace AduosSyncServices.Contracts.DTOs.GaskaApi
{
    public class GaskaGetProductsRequest
    {
        public int? CategoryId { get; set; }
        public int PageSize { get; set; }
        public string Language { get; set; }
        public int Page { get; set; }
    }
}
