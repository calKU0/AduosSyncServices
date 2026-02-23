using AduosSyncServices.Contracts.Models;

namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IParameterRepository
    {
        Task SaveProductParametersAsync(List<ProductParameter> parameters, CancellationToken ct);
        Task UpdateParameter(int id, int parameterId, string value, CancellationToken ct);
        Task DeleteParameter(string categoryParameterName, int productId, CancellationToken ct);
    }
}