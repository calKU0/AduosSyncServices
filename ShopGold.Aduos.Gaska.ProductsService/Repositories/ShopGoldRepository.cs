using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Data;
using AduosSyncServices.Infrastructure.Settings;
using Dapper;
using Microsoft.Extensions.Options;
using ShopGold.Aduos.Gaska.ProductsService.Models;
using System.Data;

namespace ShopGold.Aduos.Gaska.ProductsService.Repositories
{
    public interface IShopGoldRepository
    {
        /// <summary>Produkty z kategorii kanału ShopGold, z kategoriami, specyfikacją, zastosowaniami i opakowaniami.</summary>
        Task<List<Product>> GetProductsAsync(CancellationToken ct);

        Task<List<ShopGoldProductState>> GetStatesAsync(CancellationToken ct);

        Task SaveStatesAsync(IEnumerable<ShopGoldProductState> states, CancellationToken ct);

        /// <summary>Ścieżki kategorii Gąski ("A > B > C") z drzewa kategorii (GaskaCategories) po id.</summary>
        Task<Dictionary<int, string>> GetGaskaCategoryPathsAsync(IEnumerable<int> ids, CancellationToken ct);
    }

    public class ShopGoldRepository : IShopGoldRepository
    {
        private const int SaveBatchSize = 2000;

        private readonly DapperContext _context;
        private readonly int _company;

        public ShopGoldRepository(DapperContext context, IOptions<RepositorySettings> options)
        {
            _context = context;
            _company = (int)options.Value.Company;
        }

        public async Task<List<Product>> GetProductsAsync(CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
                "ShopGold_GetProducts",
                new { IntegrationCompany = _company },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 900,
                cancellationToken: ct));

            var products = (await grid.ReadAsync<Product>()).ToList();
            var categories = (await grid.ReadAsync<ProductCategory>()).ToLookup(c => c.ProductId);
            var specifications = (await grid.ReadAsync<ProductSpecification>()).ToLookup(s => s.ProductId);
            var applications = (await grid.ReadAsync<ProductApplication>()).ToLookup(a => a.ProductId);
            var packages = (await grid.ReadAsync<ProductPackage>()).ToLookup(p => p.ProductId);

            foreach (var product in products)
            {
                product.Categories = categories[product.Id].ToList();
                product.Specifications = specifications[product.Id].ToList();
                product.Applications = applications[product.Id].ToList();
                product.Packages = packages[product.Id].ToList();
            }

            return products;
        }

        public async Task<List<ShopGoldProductState>> GetStatesAsync(CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            var states = await connection.QueryAsync<ShopGoldProductState>(new CommandDefinition(
                "ShopGoldProducts_GetAll",
                new { IntegrationCompany = _company },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 900,
                cancellationToken: ct));

            return states.ToList();
        }

        public async Task<Dictionary<int, string>> GetGaskaCategoryPathsAsync(IEnumerable<int> ids, CancellationToken ct)
        {
            var list = ids.Distinct().ToList();
            if (list.Count == 0)
                return new Dictionary<int, string>();

            using var connection = _context.CreateConnection();
            connection.Open();

            var rows = await connection.QueryAsync<(int Id, string Path)>(new CommandDefinition(
                "SELECT Id, Path FROM dbo.GaskaCategories WHERE Id IN @Ids",
                new { Ids = list },
                cancellationToken: ct));

            return rows.ToDictionary(r => r.Id, r => r.Path);
        }

        public async Task SaveStatesAsync(IEnumerable<ShopGoldProductState> states, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            var unique = states
                .GroupBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last());

            foreach (var batch in unique.Chunk(SaveBatchSize))
            {
                var table = new DataTable();
                table.Columns.Add("Code", typeof(string));
                table.Columns.Add("ProductId", typeof(int));
                table.Columns.Add("FieldsJson", typeof(string));
                table.Columns.Add("ImagesHash", typeof(string));
                table.Columns.Add("IsActive", typeof(bool));

                foreach (var s in batch)
                    table.Rows.Add(s.Code, (object?)s.ProductId ?? DBNull.Value, s.FieldsJson, (object?)s.ImagesHash ?? DBNull.Value, s.IsActive);

                await connection.ExecuteAsync(new CommandDefinition(
                    "ShopGoldProducts_UpsertBatch",
                    new
                    {
                        IntegrationCompany = _company,
                        Items = table.AsTableValuedParameter("dbo.ShopGoldProductStateType")
                    },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 900,
                    cancellationToken: ct));
            }
        }
    }
}
