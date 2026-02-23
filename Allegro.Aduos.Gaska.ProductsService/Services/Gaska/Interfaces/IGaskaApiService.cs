namespace Allegro.Aduos.Gaska.ProductsService.Services.Gaska.Interfaces
{
    public interface IGaskaApiService
    {
        Task SyncProducts(CancellationToken ct = default);

        Task SyncProductDetails(CancellationToken ct = default);
    }
}