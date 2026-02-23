namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IAllegroImageService
    {
        Task ImportImages(CancellationToken ct = default);
    }
}