using AduosSyncServices.Contracts.Models;

namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IProductRepository
    {
        Task<List<Product>> GetProductsForDetailUpdate(int limit, CancellationToken ct);
        Task UpsertProductsBatchAsync(List<Product> product, CancellationToken ct);
        Task<bool> UpsertProductAsync(Product product, CancellationToken ct);
        Task<bool> UpdateProductStockAsync(string productCode, int stock, CancellationToken ct);
        Task<bool> DeleteProduct(int productId, CancellationToken ct);

        Task<List<Product>> GetProductsToUpload(int minProductStock, decimal minProductPrice, CancellationToken ct);

        Task<List<Product>> GetAllProducts(CancellationToken ct);

        Task<List<Product>> GetProductsWithoutDefaultCategory(CancellationToken ct);

        Task<List<Product>> GetProductsToUpdateParameters(CancellationToken ct);

        Task UpdateProductAllegroCategory(int productId, int categoryId, CancellationToken ct);

        Task UpdateProductAllegroCategory(string code, string categoryId, CancellationToken ct);

        Task<List<Product>> GetNotExistingProductsInAllegro(CancellationToken ct);
        Task UpdateCompatibilitySet(int productId, bool value, CancellationToken ct);

        Task UpdateProductAllegroId(int productId, string allegroProductId, string allegroCategoryId, CancellationToken ct);

        Task<int> DeleteProductsNotInIntegrationIdsAsync(IEnumerable<int> integrationIds, CancellationToken ct);
    }
}