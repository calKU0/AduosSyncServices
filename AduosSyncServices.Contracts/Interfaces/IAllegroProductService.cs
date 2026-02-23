namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IAllegroProductService
    {
        Task SearchProducts(CancellationToken ct = default);
    }
}