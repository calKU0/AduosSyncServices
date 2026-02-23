using Microsoft.Data.SqlClient;
using System.Data;

namespace AduosSyncServices.Infrastructure.Data
{
    public class DapperContext
    {
        private readonly string _connectionString;

        public DapperContext(string connectionString)
        {
            _connectionString = connectionString
                ?? throw new InvalidOperationException("Connection string 'MyDbContext' not found.");
        }

        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
    }
}