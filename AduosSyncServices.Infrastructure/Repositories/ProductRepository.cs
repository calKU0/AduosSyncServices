using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Data;
using AduosSyncServices.Infrastructure.Settings;
using Dapper;
using Microsoft.Extensions.Options;
using System.Data;
using System.Text.RegularExpressions;

namespace AduosSyncServices.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly DapperContext _context;
        private readonly int _company;
        private readonly int _account;

        public ProductRepository(DapperContext context, IOptions<RepositorySettings> options)
        {
            _context = context;
            _company = (int)options.Value.Company;
            _account = (int)options.Value.Account;
        }

        public async Task<List<Product>> GetProductsForDetailUpdate(int limit, CancellationToken ct)
        {
            using var conn = _context.CreateConnection();
            return (await conn.QueryAsync<Product>(
                "Products_GetForDetailUpdate",
                new { Limit = limit, IntegrationCompany = _company },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 900)).ToList();
        }

        public async Task<bool> DeleteProduct(int productId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task UpsertProductsBatchAsync(List<Product> products, CancellationToken ct)
        {
            if (products == null || products.Count == 0)
                return;

            var productsToUpsert = products
                .Where(p => !string.IsNullOrWhiteSpace(p.Code))
                .GroupBy(p => p.Code.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToList();

            foreach (var product in productsToUpsert)
            {
                product.Code = product.Code.Trim();
            }

            if (productsToUpsert.Count == 0)
                return;

            using var connection = _context.CreateConnection();
            connection.Open();

            var codes = productsToUpsert
                .Select(p => p.Code)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingRows = (await connection.QueryAsync<(string Code, string? Substitutes, string? RootBrand)>(
                @"SELECT rp.Code, rp.Substitutes, pa.Name AS RootBrand
                  FROM Products rp
                  LEFT JOIN ProductApplications pa ON pa.ProductId = rp.Id AND pa.ParentID = 0
                  WHERE rp.IntegrationCompany = @IntegrationCompany
                    AND rp.Code IN @Codes",
                new { IntegrationCompany = _company, Codes = codes })).ToList();

            var substitutesByCode = existingRows
                .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Substitutes).FirstOrDefault(), StringComparer.OrdinalIgnoreCase);

            var rootBrandsByCode = existingRows
                .Where(x => !string.IsNullOrWhiteSpace(x.RootBrand))
                .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.RootBrand!).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            var table = new DataTable();
            table.Columns.Add("Code", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("SupplierLogo", typeof(string));
            table.Columns.Add("SupplierName", typeof(string));
            table.Columns.Add("Description", typeof(string));
            table.Columns.Add("CustomerCode", typeof(string));
            table.Columns.Add("Ean", typeof(string));
            table.Columns.Add("InStock", typeof(double));
            table.Columns.Add("Weight", typeof(double));
            table.Columns.Add("Fits", typeof(string));
            table.Columns.Add("Unit", typeof(string));
            table.Columns.Add("CurrencyPrice", typeof(string));
            table.Columns.Add("Substitutes", typeof(string));
            table.Columns.Add("IntegrationCompany", typeof(int));
            table.Columns.Add("IntegrationId", typeof(int));
            table.Columns.Add("DeliveryType", typeof(int));
            table.Columns.Add("PriceNet", typeof(decimal));
            table.Columns.Add("PriceGross", typeof(decimal));
            table.Columns.Add("Package", typeof(decimal));

            foreach (var product in productsToUpsert)
            {
                rootBrandsByCode.TryGetValue(product.Code, out var rootBrands);
                substitutesByCode.TryGetValue(product.Code, out var existingSubstitutes);
                var substitutes = product.Substitutes ?? existingSubstitutes;

                product.Name = FixName(
                    product.Name,
                    product.Code,
                    product.SupplierName,
                    rootBrands,
                    substitutes?.Split(',').Distinct().ToList());

                table.Rows.Add(
                    product.Code,
                    product.Name ?? string.Empty,
                    product.SupplierLogo ?? (object)DBNull.Value,
                    product.SupplierName ?? (object)DBNull.Value,
                    product.Description ?? (object)DBNull.Value,
                    product.CustomerCode ?? (object)DBNull.Value,
                    product.Ean ?? (object)DBNull.Value,
                    Convert.ToDouble(product.InStock),
                    Convert.ToDouble(product.Weight),
                    product.Fits ?? (object)DBNull.Value,
                    product.Unit ?? (object)DBNull.Value,
                    product.CurrencyPrice ?? (object)DBNull.Value,
                    substitutes ?? (object)DBNull.Value,
                    _company,
                    product.IntegrationId,
                    product.DeliveryType,
                    product.PriceNet,
                    product.PriceGross,
                    product.Package);
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    "Products_UpsertBatch",
                    new { Products = table.AsTableValuedParameter("dbo.ProductUpsertType") },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 900,
                    cancellationToken: ct));
        }

        public async Task<bool> UpsertProductAsync(Product product, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var rootBrands = product.Applications?
                    .Where(a => a.ParentID == 0)
                    .Select(a => a.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList() ??

                (await connection.QueryAsync<string>(
                    "SELECT pa.Name FROM ProductApplications pa JOIN Products rp on rp.Id = pa.ProductId WHERE rp.Code = @ProductCode AND pa.ParentID = 0 AND IntegrationCompany = @IntegrationCompany",
                    new { ProductCode = product.Code, IntegrationCompany = _company },
                    transaction))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                var substitues = product.Substitutes ?? await connection.ExecuteScalarAsync<string>(
                  "SELECT Substitutes FROM Products WHERE Code = @ProductCode AND NULLIF(Substitutes,'') is not null AND IntegrationCompany = @IntegrationCompany",
                  new { ProductCode = product.Code, IntegrationCompany = _company },
                  transaction);

                product.Name = FixName(
                    product.Name,
                    product.Code,
                    product.SupplierName,
                    rootBrands,
                    substitues?.Split(',').ToList()
                );

                var productId = await connection.ExecuteScalarAsync<int>(
                    "Products_Upsert",
                    new
                    {
                        Code = product.Code,
                        Name = product.Name,
                        SupplierLogo = product.SupplierLogo,
                        SupplierName = product.SupplierName,
                        Description = product.Description,
                        CustomerCode = product.CustomerCode,
                        Ean = product.Ean,
                        InStock = product.InStock,
                        Weight = product.Weight,
                        Fits = product.Fits,
                        Unit = product.Unit,
                        Currency = product.CurrencyPrice,
                        Substitutes = product.Substitutes,
                        IntegrationCompany = _company,
                        IntegrationId = product.IntegrationId,
                        DeliveryType = product.DeliveryType,
                        PriceNet = product.PriceNet,
                        PriceGross = product.PriceGross,
                        Package = product.Package
                    },
                    transaction,
                    commandType: CommandType.StoredProcedure
                );

                if (product.Specifications?.Any() == true)
                {
                    await ReplaceSpecificationsAsync(connection, transaction, productId, product.Specifications, ct);
                }

                if (product.Categories?.Any() == true)
                {
                    await ReplaceCategoriesAsync(connection, transaction, productId, product.Categories, ct);
                }

                if (product.Packages?.Any() == true)
                {
                    await ReplacePackagesAsync(connection, transaction, productId, product.Packages, ct);
                }

                if (product.Applications?.Any() == true)
                {
                    await ReplaceApplicationsAsync(connection, transaction, productId, product.Applications, ct);
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private async Task ReplaceSpecificationsAsync(IDbConnection connection, IDbTransaction transaction, int productId, List<ProductSpecification> specifications, CancellationToken ct)
        {
            var table = new DataTable();
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Value", typeof(string));
            table.Columns.Add("UnitName", typeof(string));

            foreach (var s in specifications)
            {
                table.Rows.Add(s.Name ?? string.Empty, s.Value ?? (object)DBNull.Value, s.UnitName ?? (object)DBNull.Value);
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    "ProductSpecifications_ReplaceByProductId",
                    new
                    {
                        ProductId = productId,
                        Items = table.AsTableValuedParameter("dbo.ProductSpecificationType")
                    },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 900,
                    cancellationToken: ct));
        }

        private async Task ReplaceCategoriesAsync(IDbConnection connection, IDbTransaction transaction, int productId, List<ProductCategory> categories, CancellationToken ct)
        {
            var table = new DataTable();
            table.Columns.Add("Name", typeof(string));

            foreach (var c in categories)
            {
                table.Rows.Add(c.Name ?? string.Empty);
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    "ProductCategories_ReplaceByProductId",
                    new
                    {
                        ProductId = productId,
                        Items = table.AsTableValuedParameter("dbo.ProductCategoriesType")
                    },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 900,
                    cancellationToken: ct));
        }

        private async Task ReplacePackagesAsync(IDbConnection connection, IDbTransaction transaction, int productId, List<ProductPackage> packages, CancellationToken ct)
        {
            var table = new DataTable();
            table.Columns.Add("PackUnit", typeof(string));
            table.Columns.Add("PackQty", typeof(double));
            table.Columns.Add("PackNettWeight", typeof(double));
            table.Columns.Add("PackGrossWeight", typeof(double));
            table.Columns.Add("PackEan", typeof(string));
            table.Columns.Add("PackRequired", typeof(int));

            foreach (var p in packages)
            {
                table.Rows.Add(
                    p.PackUnit ?? (object)DBNull.Value,
                    Convert.ToDouble(p.PackQty),
                    Convert.ToDouble(p.PackNettWeight),
                    Convert.ToDouble(p.PackGrossWeight),
                    p.PackEan ?? (object)DBNull.Value,
                    p.PackRequired);
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    "ProductPackages_ReplaceByProductId",
                    new
                    {
                        ProductId = productId,
                        Items = table.AsTableValuedParameter("dbo.ProductPackageType")
                    },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 900,
                    cancellationToken: ct));
        }

        private async Task ReplaceApplicationsAsync(IDbConnection connection, IDbTransaction transaction, int productId, List<ProductApplication> applications, CancellationToken ct)
        {
            var table = new DataTable();
            table.Columns.Add("ApplicationId", typeof(int));
            table.Columns.Add("ParentID", typeof(int));
            table.Columns.Add("Name", typeof(string));

            foreach (var a in applications)
            {
                table.Rows.Add(a.ApplicationId, a.ParentID, a.Name ?? (object)DBNull.Value);
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    "ProductApplications_ReplaceByProductId",
                    new
                    {
                        ProductId = productId,
                        Items = table.AsTableValuedParameter("dbo.ProductApplicationType")
                    },
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 900,
                    cancellationToken: ct));
        }

        public async Task<List<Product>> GetProductsWithoutDefaultCategory(CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            var productDict = new Dictionary<int, Product>();

            await connection.QueryAsync<
                Product,
                ProductSpecification,
                Product>(
                "Products_GetWithoutDefaultCategory",
                (product, spec) =>
                {
                    if (!productDict.TryGetValue(product.Id, out var existing))
                    {
                        existing = product;
                        existing.Specifications = new List<ProductSpecification>();
                        existing.Parameters = new List<ProductParameter>();

                        productDict.Add(existing.Id, existing);
                    }

                    if (spec?.Id > 0 && !existing.Specifications.Any(s => s.Id == spec.Id))
                        existing.Specifications.Add(spec);

                    return existing;
                },
                new { IntegrationCompany = _company },
                splitOn: "Id",
                commandTimeout: 900,
                commandType: CommandType.StoredProcedure
            );

            return productDict.Values.ToList();
        }

        public async Task<List<Product>> GetProductsToUpdateParameters(CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            var productDict = new Dictionary<int, Product>();

            await connection.QueryAsync<
                Product,
                ProductApplication,
                ProductSpecification,
                Product>(
                "Products_GetToUpdateParameters",
                (product, application, spec) =>
                {
                    if (!productDict.TryGetValue(product.Id, out var existing))
                    {
                        existing = product;
                        existing.Applications = new List<ProductApplication>();
                        existing.Specifications = new List<ProductSpecification>();
                        existing.Parameters = new List<ProductParameter>();

                        productDict.Add(existing.Id, existing);
                    }

                    if (application?.Id > 0 && !existing.Applications.Any(a => a.Id == application.Id))
                        existing.Applications.Add(application);

                    if (spec?.Id > 0 && !existing.Specifications.Any(s => s.Id == spec.Id))
                        existing.Specifications.Add(spec);

                    return existing;
                },
                new { IntegrationCompany = _company },
                splitOn: "Id,Id",
                commandTimeout: 900,
                commandType: CommandType.StoredProcedure
            );

            return productDict.Values.ToList();
        }

        public async Task UpdateProductAllegroCategory(int productId, int categoryId, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            await connection.ExecuteAsync(
                "Products_UpdateDefaultCategoryById",
                new
                {
                    ProductId = productId,
                    CategoryId = categoryId
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task UpdateProductAllegroCategory(string productCode, string categoryId, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            await connection.ExecuteAsync(
                "Products_UpdateDefaultCategoryByCode",
                new
                {
                    ProductCode = productCode,
                    CategoryId = categoryId
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<List<Product>> GetProductsToUpload(int minProductStock, decimal minProductPrice, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            var productDict = new Dictionary<int, Product>();

            await connection.QueryAsync<
                Product,
                ProductSpecification,
                ProductParameter,
                ProductApplication,
                ProductPackage,
                Product>(
                "Products_GetToUpload",
                (product, spec, param, application, package) =>
                {
                    if (!productDict.TryGetValue(product.Id, out var existing))
                    {
                        existing = product;
                        existing.Specifications = new List<ProductSpecification>();
                        existing.Parameters = new List<ProductParameter>();
                        existing.Applications = new List<ProductApplication>();
                        existing.Packages = new List<ProductPackage>();

                        productDict.Add(existing.Id, existing);
                    }

                    if (spec?.Id > 0 && !existing.Specifications.Any(s => s.Id == spec.Id))
                        existing.Specifications.Add(spec);

                    if (param?.Id > 0 && !existing.Parameters.Any(p => p.Id == param.Id))
                        existing.Parameters.Add(param);

                    if (application?.Id > 0 && !existing.Applications.Any(p => p.Id == application.Id))
                        existing.Applications.Add(application);

                    if (package?.Id > 0 && !existing.Packages.Any(p => p.Id == package.Id))
                        existing.Packages.Add(package);

                    return existing;
                },
                new { MinProductStock = minProductStock, MinProductPrice = minProductPrice, IntegrationCompany = _company, Account = _account },
                splitOn: "Id,Id,Id,Id",
                commandTimeout: 900,
                commandType: CommandType.StoredProcedure
            );

            return productDict.Values.ToList();
        }

        private string FixName(string name, string code, string? supplierName, List<string>? rootBrands = null, List<string>? crossNumbers = null)
        {
            name = NormalizeNameValue(name);
            code = NormalizeNameValue(code);
            supplierName = NormalizeNameValue(supplierName);

            name = Regex.Replace(name, @"\S{30,}", m =>
            {
                var word = m.Value;
                int splitPos = -1;

                for (int i = Math.Min(35, word.Length - 1); i >= Math.Max(20, 1); i--)
                {
                    if (word[i] == '-' || word[i] == '/' || word[i] == '_' || word[i] == '.')
                    {
                        splitPos = i + 1;
                        break;
                    }
                }

                if (splitPos == -1)
                    splitPos = 30;

                return word.Substring(0, splitPos) + " " + word.Substring(splitPos);
            });

            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            while (name.Length > 75)
            {
                if (parts.Count <= 1) break;
                parts.RemoveAt(parts.Count - 1);
                name = string.Join(" ", parts);
            }

            if (parts.Count < 3)
            {
                if (!string.IsNullOrEmpty(code))
                    parts.Add(code);

                if (parts.Count < 3 && !string.IsNullOrEmpty(supplierName))
                    parts.Add(supplierName);

                if (parts.Count < 3 && crossNumbers != null && crossNumbers.Count > 0)
                    parts.Add(crossNumbers[0]);

                if (parts.Count < 3 && rootBrands != null && rootBrands.Count > 0)
                    parts.Add(rootBrands[0]);

                if (parts.Count < 3)
                    parts.Add("JAG");

                name = string.Join(" ", parts);
            }

            return NormalizeNameValue(name).ToUpperInvariant();
        }

        private static string NormalizeNameValue(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var noControls = new string(input
                .Select(c => char.IsControl(c) ? ' ' : c)
                .ToArray());

            var noInvalidChars = Regex.Replace(noControls, @"[^\p{L}\p{N}\s\-\._/,+]", " ");

            return Regex.Replace(noInvalidChars, @"\s+", " ").Trim();
        }

        public async Task UpdateCompatibilitySet(int productId, bool value, CancellationToken ct)
        {
            var sql = @"
                UPDATE Products
                SET BuildCompatibilitySet = @Value
                WHERE Id = @ProductId;";

            using var conn = _context.CreateConnection();
            conn.Open();
            await conn.ExecuteAsync(sql, new { Value = value, ProductId = productId });
        }

        public Task<bool> UpdateProductStockAsync(string productCode, int stock, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<List<Product>> GetAllProducts(CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            var products = await connection.QueryAsync<Product>(
                "Products_GetAll",
                new { IntegrationCompany = _company },
                commandTimeout: 900,
                commandType: CommandType.StoredProcedure);

            return products.ToList();
        }

        public Task<List<Product>> GetNotExistingProductsInAllegro(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task UpdateProductAllegroId(int productId, string allegroProductId, string allegroCategoryId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<int> DeleteProductsNotInIntegrationIdsAsync(IEnumerable<int> integrationIds, CancellationToken ct)
        {
            var ids = integrationIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!ids.Any())
                return 0;

            using var connection = _context.CreateConnection();
            connection.Open();

            var syncRunId = Guid.NewGuid();
            var totalDeleted = 0;

            try
            {
                foreach (var batch in ids.Chunk(2000))
                {
                    var table = new DataTable();
                    table.Columns.Add("IntegrationId", typeof(int));

                    foreach (var id in batch)
                    {
                        table.Rows.Add(id);
                    }

                    var insertCommand = new CommandDefinition(
                        "ProductSyncStaging_InsertBatch",
                        new
                        {
                            SyncRunId = syncRunId,
                            IntegrationCompany = _company,
                            Items = table.AsTableValuedParameter("dbo.ProductIntegrationIdType")
                        },
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: 900,
                        cancellationToken: ct);

                    await connection.ExecuteAsync(insertCommand);
                }

                while (true)
                {
                    var deleteBatchCommand = new CommandDefinition(
                        "Products_DeleteMissingBySyncRun",
                        new
                        {
                            SyncRunId = syncRunId,
                            IntegrationCompany = _company,
                            BatchSize = 10000
                        },
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: 900,
                        cancellationToken: ct);

                    var deletedInBatch = await connection.ExecuteScalarAsync<int>(deleteBatchCommand);
                    if (deletedInBatch <= 0)
                        break;

                    totalDeleted += deletedInBatch;
                }

                return totalDeleted;
            }
            finally
            {
                var cleanupCommand = new CommandDefinition(
                    "ProductSyncStaging_ClearRun",
                    new { SyncRunId = syncRunId, IntegrationCompany = _company },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 900,
                    cancellationToken: ct);

                await connection.ExecuteAsync(cleanupCommand);
            }
        }
    }
}
