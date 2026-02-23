namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IAllegroParametersService
    {
        Task UpdateParameters(CancellationToken ct = default);
    }
}