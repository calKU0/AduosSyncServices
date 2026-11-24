namespace AduosSyncServices.Contracts.DTOs.GaskaApi
{
    /// <summary>Odpowiedź endpointu /categories - płaska lista kategorii z odwołaniem do rodzica.</summary>
    public class GaskaGetCategoriesResponse
    {
        public List<ApiCategory> Categories { get; set; } = new();

        public int Result { get; set; }

        public string? Message { get; set; }
    }
}
