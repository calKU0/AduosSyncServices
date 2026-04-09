using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Data;
using Dapper;
using System.Data;
using System.Text.Json;

namespace AduosSyncServices.Infrastructure.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly DapperContext _context;

        public CategoryRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task SaveCategoryTreeAsync(CategoryDto category, CancellationToken ct)
        {
            var stack = new Stack<CategoryDto>();
            var current = category;

            while (current != null)
            {
                stack.Push(current);
                current = current.Parent;
            }

            AllegroCategory parentEntity = null;

            using var conn = _context.CreateConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();

            while (stack.Any())
            {
                var dto = stack.Pop();

                var id = await conn.ExecuteScalarAsync<int>(
                    "AllegroCategories_Upsert",
                    new { CategoryId = dto.Id, Name = dto.Name, ParentId = parentEntity?.Id },
                    tran,
                    commandType: CommandType.StoredProcedure
                );

                parentEntity = new AllegroCategory { Id = id, CategoryId = dto.Id, Name = dto.Name, ParentId = parentEntity?.Id };
            }

            tran.Commit();
        }

        public async Task<IEnumerable<CategoryParameter>> GetCategoryParametersAsync(int categoryId, CancellationToken ct = default)
        {
            using var conn = _context.CreateConnection();

            var paramDict = new Dictionary<int, CategoryParameter>();

            await conn.QueryAsync<CategoryParameter, CategoryParameterValue, CategoryParameter>(
                "CategoryParameters_GetByCategoryId",
                (cp, cpv) =>
                {
                    if (!paramDict.TryGetValue(cp.Id, out var categoryParam))
                    {
                        categoryParam = cp;
                        categoryParam.Values = new List<CategoryParameterValue>();
                        paramDict.Add(cp.Id, categoryParam);
                    }

                    if (cpv != null && !string.IsNullOrEmpty(cpv.Value))
                        categoryParam.Values.Add(cpv);

                    return categoryParam;
                },
                new { CategoryId = categoryId, OnlyForOffers = 0 },
                splitOn: "ValueId",
                commandType: CommandType.StoredProcedure
            );

            return paramDict.Values;
        }

        public async Task SaveCategoryParametersAsync(IEnumerable<CategoryParameter> parameters, CancellationToken ct)
        {
            if (parameters == null || !parameters.Any())
                return;

            using var conn = _context.CreateConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();

            foreach (var param in parameters)
            {
                var valuesJson = param.Values != null && param.Values.Any()
                    ? JsonSerializer.Serialize(param.Values.Select(v => v.Value))
                    : null;

                await conn.ExecuteAsync(
                    "CategoryParameters_UpsertWithValues",
                    new
                    {
                        param.CategoryId,
                        param.ParameterId,
                        param.Name,
                        param.Type,
                        param.Required,
                        param.Min,
                        param.Max,
                        param.RequiredForProduct,
                        param.DescribesProduct,
                        param.CustomValuesEnabled,
                        param.AmbiguousValueId,
                        ValuesJson = valuesJson
                    },
                    tran,
                    commandType: CommandType.StoredProcedure
                );
            }

            tran.Commit();
        }

        public async Task<IEnumerable<int>> GetDefaultCategories(CancellationToken ct)
        {
            using var conn = _context.CreateConnection();
            var result = await conn.QueryAsync<int>(
                "Products_GetDefaultAllegroCategoriesWithoutParameters",
                commandType: CommandType.StoredProcedure);
            return result;
        }

        public async Task<int?> GetMostCommonDefaultAllegroCategory(int productId, CancellationToken ct)
        {
            using var conn = _context.CreateConnection();

            var productCategories = (await conn.QueryAsync<ProductCategory>(
                "SELECT * FROM ProductCategories WHERE ProductId = @ProductId", new { ProductId = productId }
            )).ToList();

            if (!productCategories.Any()) return null;

            var root = productCategories.Select(c => c.Name).FirstOrDefault(c => c.Contains("Części według rodzaju"))
                       ?? productCategories.Select(c => c.Name).First();

            if (root == null) return null;

            var stats = await conn.QueryFirstOrDefaultAsync<(int CategoryId, int Count)>(
                @"SELECT p.DefaultAllegroCategory AS CategoryId, COUNT(*) AS Count
                    FROM ProductCategories pc
                    INNER JOIN Products p ON p.Id = pc.ProductId
                    WHERE pc.Name = @Name AND pc.ProductId != @ProductId AND p.DefaultAllegroCategory != 0
                    GROUP BY p.DefaultAllegroCategory
                    ORDER BY COUNT(*) DESC",
                new { Name = root, ProductId = productId }
            );

            if (stats.CategoryId != 0) return stats.CategoryId;

            var nameLower = root.ToLower();
            if (nameLower.Contains("traktor")) return 305829;
            if (nameLower.Contains("kombajn")) return 319159;

            return null;
        }

        public async Task<IEnumerable<AllegroCategory>> GetAllegroCategories(CancellationToken ct)
        {
            using var conn = _context.CreateConnection();
            return await conn.QueryAsync<AllegroCategory>(
                "AllegroCategories_GetAll",
                commandType: CommandType.StoredProcedure);
        }
    }
}
