using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Data;
using Dapper;
using System.Data;

namespace AduosSyncServices.Infrastructure.Repositories
{
    public class OrderInternalStatusRepository : IOrderInternalStatusRepository
    {
        private readonly DapperContext _context;

        public OrderInternalStatusRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<List<OrderInternalStatus>> GetAll(CancellationToken ct = default)
        {
            using var connection = _context.CreateConnection();
            var command = new CommandDefinition(
                "dbo.OrderInternalStatuses_GetAll",
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct);

            return (await connection.QueryAsync<OrderInternalStatus>(command)).ToList();
        }

        public async Task<int> Add(string name, string color, CancellationToken ct = default)
        {
            using var connection = _context.CreateConnection();
            var parameters = new DynamicParameters();
            parameters.Add("@Name", name);
            parameters.Add("@Color", color);
            parameters.Add("@Id", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await connection.ExecuteAsync(new CommandDefinition(
                "dbo.OrderInternalStatuses_Add",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));

            return parameters.Get<int>("@Id");
        }

        public async Task Update(int id, string name, string color, CancellationToken ct = default)
        {
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(new CommandDefinition(
                "dbo.OrderInternalStatuses_Update",
                new { Id = id, Name = name, Color = color },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
        }

        public async Task Delete(int id, CancellationToken ct = default)
        {
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(new CommandDefinition(
                "dbo.OrderInternalStatuses_Delete",
                new { Id = id },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
        }
    }
}
