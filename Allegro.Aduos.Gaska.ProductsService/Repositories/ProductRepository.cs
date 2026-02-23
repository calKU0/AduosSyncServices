using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Data;
using Allegro.Aduos.Gaska.ProductsService.Constants;
using Dapper;
using System.Data;
using System.Text.RegularExpressions;

namespace Allegro.Aduos.Gaska.ProductsService.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly DapperContext _context;
        private readonly ILogger<ProductRepository> _logger;

        public ProductRepository(DapperContext context, ILogger<ProductRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Product>> GetProductsForDetailUpdate(int limit, CancellationToken ct)
        {
            using var conn = _context.CreateConnection();
            return (await conn.QueryAsync<Product>(
                "Products_GetForDetailUpdate",
                new { Limit = limit, IntegrationCompany = ServiceConstants.Company },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 900)).ToList();
        }

        public async Task<bool> DeleteProduct(int productId, CancellationToken ct)
        {
            throw new NotImplementedException();
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
                    new { ProductCode = product.Code, IntegrationCompany = ServiceConstants.Company },
                    transaction))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                var substitues = product.Substitutes ?? await connection.ExecuteScalarAsync<string>(
                  "SELECT Substitutes FROM Products WHERE Code = @ProductCode AND NULLIF(Substitutes,'') is not null AND IntegrationCompany = @IntegrationCompany",
                  new { ProductCode = product.Code, IntegrationCompany = ServiceConstants.Company },
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
                        IntegrationCompany = ServiceConstants.Company,
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
                    // Replace specifications
                    await connection.ExecuteAsync(
                        "ProductSpecifications_DeleteByProductId",
                        new { ProductId = productId },
                        transaction,
                        commandType: CommandType.StoredProcedure
                    );

                    var specs = product.Specifications.Select(s => new
                    {
                        ProductId = productId,
                        s.Name,
                        s.Value,
                        s.UnitName
                    });

                    await connection.ExecuteAsync(
                        "ProductSpecifications_Insert",
                        specs,
                        transaction,
                        commandType: CommandType.StoredProcedure
                    );
                }

                if (product.Categories?.Any() == true)
                {
                    // Replace categories
                    await connection.ExecuteAsync(
                        "ProductCategories_DeleteByProductId",
                        new { ProductId = productId },
                        transaction,
                        commandType: CommandType.StoredProcedure
                    );

                    var categories = product.Categories.Select(c => new
                    {
                        ProductId = productId,
                        Name = c.Name
                    });

                    await connection.ExecuteAsync(
                        "ProductCategories_Insert",
                        categories,
                        transaction,
                        commandType: CommandType.StoredProcedure
                    );
                }

                if (product.Packages?.Any() == true)
                {
                    // Replace packages
                    await connection.ExecuteAsync(
                        "ProductPackages_DeleteByProductId",
                        new { ProductId = productId },
                        transaction,
                        commandType: CommandType.StoredProcedure
                    );

                    var packages = product.Packages.Select(p => new
                    {
                        ProductId = productId,
                        p.PackUnit,
                        p.PackQty,
                        p.PackNettWeight,
                        p.PackGrossWeight,
                        p.PackEan,
                        p.PackRequired
                    });

                    await connection.ExecuteAsync(
                        "ProductPackages_Insert",
                        packages,
                        transaction,
                        commandType: CommandType.StoredProcedure
                    );
                }

                if (product.Applications?.Any() == true)
                {
                    // Replace applications
                    await connection.ExecuteAsync(
                        "ProductApplications_DeleteByProductId",
                        new { ProductId = productId },
                        transaction,
                        commandType: CommandType.StoredProcedure
                    );

                    var applications = product.Applications.Select(a => new
                    {
                        ProductId = productId,
                        a.ApplicationId,
                        a.ParentID,
                        a.Name
                    });

                    await connection.ExecuteAsync(
                        "ProductApplications_Insert",
                        applications,
                        transaction,
                        commandType: CommandType.StoredProcedure
                    );
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
                        existing.Parameters = new List<ProductParameter>(); // puste

                        productDict.Add(existing.Id, existing);
                    }

                    if (spec?.Id > 0 && !existing.Specifications.Any(s => s.Id == spec.Id))
                        existing.Specifications.Add(spec);

                    return existing;
                },
                new { IntegrationCompany = ServiceConstants.Company },
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
                new { IntegrationCompany = ServiceConstants.Company },
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
            var affectedRows = await connection.ExecuteAsync(
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
            var affectedRows = await connection.ExecuteAsync(
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
                new { MinProductStock = minProductStock, MinProductPrice = minProductPrice, IntegrationCompany = ServiceConstants.Company, Account = ServiceConstants.Account },
                splitOn: "Id,Id,Id,Id",
                commandTimeout: 900,
                commandType: CommandType.StoredProcedure
            );

            return productDict.Values.ToList();
        }

        private string FixName(string name, string code, string? supplierName, List<string>? rootBrands = null, List<string>? crossNumbers = null)
        {
            // Insert space inside words longer than or equal to 30 characters
            name = Regex.Replace(name, @"\S{30,}", m =>
            {
                var word = m.Value;

                // Try to find a natural split near position 30
                int splitPos = -1;

                // look for separator between 20 and 35 chars
                for (int i = Math.Min(35, word.Length - 1); i >= Math.Max(20, 1); i--)
                {
                    if (word[i] == '-' || word[i] == '/' || word[i] == '_' || word[i] == '.')
                    {
                        splitPos = i + 1;
                        break;
                    }
                }

                // fallback: hard split at 30
                if (splitPos == -1)
                    splitPos = 30;

                return word.Substring(0, splitPos) + " " + word.Substring(splitPos);
            });

            // If longer than 75 chars → remove last words until < 75
            while (name.Length > 75)
            {
                var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (parts.Count <= 1) break; // stop if only 1 word left
                parts.RemoveAt(parts.Count - 1);
                name = string.Join(" ", parts);
            }

            // Ensure name is at least 3 characters
            if (name.Length < 3)
            {
                if (!string.IsNullOrEmpty(code))
                    name += code;

                if (name.Length < 3 && !string.IsNullOrEmpty(supplierName))
                    name += supplierName;

                if (name.Length < 3 && crossNumbers != null && crossNumbers.Count > 0)
                    name += crossNumbers[0];

                if (name.Length < 3 && rootBrands != null && rootBrands.Count > 0)
                    name += rootBrands[0];

                if (name.Length < 3)
                    name += "JAG";
            }

            return name;
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
                new { IntegrationCompany = ServiceConstants.Company },
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
    }
}