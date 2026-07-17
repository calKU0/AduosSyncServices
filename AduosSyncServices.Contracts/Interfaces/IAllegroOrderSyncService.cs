namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IAllegroOrderSyncService
    {
        Task SyncOrdersFromAllegro(List<string> allegroDeliveryNames, IProgress<string>? progress = null, CancellationToken ct = default);
        Task UpdateOrderGaskaInfo(IProgress<string>? progress = null, CancellationToken ct = default);
        Task UpdateOrdersInAllegro(IProgress<string>? progress = null, CancellationToken ct = default);
    }
}
