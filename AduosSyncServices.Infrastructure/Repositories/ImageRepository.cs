using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Infrastructure.Data;
using AduosSyncServices.Infrastructure.Settings;
using Dapper;
using Microsoft.Extensions.Options;
using System.Data;

namespace AduosSyncServices.Infrastructure.Repositories
{
    public class ImageRepository : IImageRepository
    {
        private readonly DapperContext _context;
        private readonly int _account;

        public ImageRepository(DapperContext context, IOptions<RepositorySettings> options)
        {
            _context = context;
            _account = (int)options.Value.Account;
        }

        public async Task<int> AddImageAsync(int productId, string url, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            return await connection.ExecuteScalarAsync<int>(
                "AllegroImages_Add",
                new { ProductId = productId, Url = url, Account = _account },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task DeleteNotConnectedImages(int productId, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            await connection.ExecuteScalarAsync<int>(
                "AllegroImages_DeleteNotConnectedByProductId",
                new { ProductId = productId, Account = _account },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<int> DeleteProductImagesAsync(int productId, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            return await connection.ExecuteScalarAsync<int>(
                "AllegroImages_DeleteByProductId",
                new { ProductId = productId, Account = _account },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task MarkImagesAsConnectedAsync(int productId, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            await connection.ExecuteAsync(
                "AllegroImages_MarkConnectedByProductId",
                new { ProductId = productId, Account = _account },
                commandType: CommandType.StoredProcedure
            );
        }
    }
}
