using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Infrastructure.Data;
using AduosSyncServices.Infrastructure.Settings;
using Dapper;
using Microsoft.Extensions.Options;
using System.Data;

namespace AduosSyncServices.Infrastructure.Repositories
{
    public class SyncCategoryRepository : ISyncCategoryRepository
    {
        private readonly DapperContext _context;
        private readonly int _company;

        public SyncCategoryRepository(DapperContext context, IOptions<RepositorySettings> options)
        {
            _context = context;
            _company = (int)options.Value.Company;
        }

        public async Task ReplaceChannelCategoriesAsync(string channel, IEnumerable<int> categoryIds, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            await connection.ExecuteAsync(new CommandDefinition(
                "SyncCategories_ReplaceByChannel",
                new
                {
                    Channel = channel,
                    IntegrationCompany = _company,
                    Categories = ToCategoryTable(categoryIds).AsTableValuedParameter("dbo.SyncCategoryIdType")
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
        }

        public async Task<List<(string Channel, int CategoryId)>> GetAllChannelCategoriesAsync(CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            var rows = await connection.QueryAsync<(string Channel, int CategoryId)>(new CommandDefinition(
                "SyncCategories_GetByCompany",
                new { IntegrationCompany = _company },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));

            return rows.ToList();
        }

        public async Task ReplaceProductLinksForCategoryAsync(int categoryId, IEnumerable<int> integrationIds, CancellationToken ct)
        {
            var table = new DataTable();
            table.Columns.Add("IntegrationId", typeof(int));

            foreach (var id in integrationIds.Where(i => i > 0).Distinct())
                table.Rows.Add(id);

            using var connection = _context.CreateConnection();
            connection.Open();

            await connection.ExecuteAsync(new CommandDefinition(
                "ProductSyncCategories_ReplaceForCategory",
                new
                {
                    IntegrationCompany = _company,
                    CategoryId = categoryId,
                    Items = table.AsTableValuedParameter("dbo.ProductIntegrationIdType")
                },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 900,
                cancellationToken: ct));
        }

        public async Task<int> DeleteProductLinksNotInAsync(IEnumerable<int> categoryIds, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "ProductSyncCategories_DeleteNotIn",
                new
                {
                    IntegrationCompany = _company,
                    Categories = ToCategoryTable(categoryIds).AsTableValuedParameter("dbo.SyncCategoryIdType")
                },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 900,
                cancellationToken: ct));
        }

        public async Task<int> UpsertGaskaCategoryTreeAsync(IEnumerable<(int Id, int ParentId, string Name)> categories, CancellationToken ct)
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("ParentId", typeof(int));
            table.Columns.Add("Name", typeof(string));

            // Id jest kluczem typu tabelarycznego - duplikat wywróciłby całe wywołanie.
            foreach (var c in categories
                .Where(c => c.Id != 0 && !string.IsNullOrWhiteSpace(c.Name))
                .DistinctBy(c => c.Id))
            {
                var parent = c.ParentId == 0 || c.ParentId == c.Id ? (object)DBNull.Value : c.ParentId;
                table.Rows.Add(c.Id, parent, c.Name.Trim());
            }

            if (table.Rows.Count == 0)
                return 0;

            using var connection = _context.CreateConnection();
            connection.Open();

            await connection.ExecuteAsync(new CommandDefinition(
                "GaskaCategories_Upsert",
                new { Nodes = table.AsTableValuedParameter("dbo.GaskaCategoryNodeType") },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 300,
                cancellationToken: ct));

            return table.Rows.Count;
        }

        private static DataTable ToCategoryTable(IEnumerable<int> categoryIds)
        {
            var table = new DataTable();
            table.Columns.Add("CategoryId", typeof(int));

            foreach (var id in (categoryIds ?? Enumerable.Empty<int>()).Where(i => i != 0).Distinct())
                table.Rows.Add(id);

            return table;
        }
    }
}
