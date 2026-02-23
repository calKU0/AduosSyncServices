namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IImageRepository
    {
        Task<int> AddImageAsync(int productId, string url, CancellationToken ct);

        Task MarkImagesAsConnectedAsync(int productId, CancellationToken ct);

        Task DeleteNotConnectedImages(int productId, CancellationToken ct);
        Task<int> DeleteProductImagesAsync(int productId, CancellationToken ct);
    }
}