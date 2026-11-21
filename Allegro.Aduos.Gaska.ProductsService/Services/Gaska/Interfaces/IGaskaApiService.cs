namespace Allegro.Aduos.Gaska.ProductsService.Services.Gaska.Interfaces
{
    public interface IGaskaApiService
    {
        Task SyncProducts(CancellationToken ct = default);

        /// <returns>False when the product list could not be loaded, so the run should be retried later.</returns>
        Task<bool> SyncProductDetails(CancellationToken ct = default);
    }
}