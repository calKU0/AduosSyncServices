using AduosSyncServices.Contracts.Models;

namespace AduosSyncServices.Contracts.Interfaces
{
    // CRUD for the global list of user-defined internal order statuses.
    public interface IOrderInternalStatusRepository
    {
        Task<List<OrderInternalStatus>> GetAll(CancellationToken ct = default);

        Task<int> Add(string name, string color, CancellationToken ct = default);

        Task Update(int id, string name, string color, CancellationToken ct = default);

        Task Delete(int id, CancellationToken ct = default);
    }
}
