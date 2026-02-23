using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Infrastructure.Data;
using Allegro.Aduos.Gaska.ProductsService.Constants;
using Dapper;
using System.Data;

namespace Allegro.Aduos.Gaska.ProductsService.Repositories
{
    public class ImageRepository : IImageRepository
    {
        private readonly DapperContext _context;

        public ImageRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<int> AddImageAsync(int productId, string url, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            return await connection.ExecuteScalarAsync<int>(
                "AllegroImages_Add",
                new { ProductId = productId, Url = url, Account = ServiceConstants.Account },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task DeleteNotConnectedImages(int productId, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            await connection.ExecuteScalarAsync<int>(
                "AllegroImages_DeleteNotConnectedByProductId",
                new { ProductId = productId, Account = ServiceConstants.Account },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<int> DeleteProductImagesAsync(int productId, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            return await connection.ExecuteScalarAsync<int>(
                "AllegroImages_DeleteByProductId",
                new { ProductId = productId, Account = ServiceConstants.Account },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task MarkImagesAsConnectedAsync(int productId, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            await connection.ExecuteAsync(
                "AllegroImages_MarkConnectedByProductId",
                new { ProductId = productId, Account = ServiceConstants.Account },
                commandType: CommandType.StoredProcedure
            );
        }
    }
}