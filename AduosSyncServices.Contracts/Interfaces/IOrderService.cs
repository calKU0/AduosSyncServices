namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IOrderService
    {
        public Task SyncOrdersFromAllegro(CancellationToken ct = default!);

        public Task CreateOrdersInGaska(CancellationToken ct = default!);

        public Task UpdateOrdersInAllegro(CancellationToken ct = default!);

        public Task UpdateOrderGaskaInfo(CancellationToken ct = default!);
    }
}