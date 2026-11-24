using AduosSyncServices.ServicesManager.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AduosSyncServices.ServicesManager.Services
{
    /// <summary>
    /// Drzewo kategorii Gąski z bazy (tabela GaskaCategories). Uzupełnia je serwis Allegro
    /// na początku każdego pobierania produktów (API /categories).
    /// </summary>
    public class GaskaCategoryCatalog
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        public async Task<IReadOnlyList<CategoryOption>> LoadAsync(string connectionString, CancellationToken ct = default)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                // Konfigurator ma odpowiadać szybko - lepiej pokazać błąd niż wisieć na połączeniu.
                ConnectTimeout = (int)Timeout.TotalSeconds
            };

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(ct);

            // Przed pierwszą migracją z drzewem kategorii procedury jeszcze nie ma.
            await using (var exists = new SqlCommand("SELECT OBJECT_ID(N'dbo.GaskaCategories_GetAll', N'P')", connection))
            {
                if (await exists.ExecuteScalarAsync(ct) is null or DBNull)
                    return Array.Empty<CategoryOption>();
            }

            await using var command = new SqlCommand("dbo.GaskaCategories_GetAll", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = (int)Timeout.TotalSeconds
            };

            var result = new List<CategoryOption>();

            await using var reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
                result.Add(new CategoryOption(reader.GetInt32(0).ToString(), reader.GetString(1), reader.GetInt32(2)));

            return result;
        }
    }
}
