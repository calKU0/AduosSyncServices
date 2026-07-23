using AduosSyncServices.Contracts.Settings;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AduosSyncServices.Infrastructure.Tests.Integration
{
    // Central config/gating for integration tests. These tests touch real external systems (a SQL
    // Server instance and the Gąska/Allegro sandbox APIs), so they are OPT-IN via environment
    // variables and skipped otherwise - a plain `dotnet test` runs only the deterministic unit tests.
    //
    //   RUN_DB_INTEGRATION_TESTS=1   -> repository tests against a dedicated Allegro_Test database
    //   RUN_API_INTEGRATION_TESTS=1  -> read-only API smoke tests against the sandbox/test accounts
    //   RUN_API_WRITE_TESTS=1        -> additionally allow low-risk writes (one-use delivery address)
    //
    // Credentials + the base connection string are read from the ProductsService appsettings.json
    // (already test/sandbox values); only the database NAME is swapped to Allegro_Test so the real
    // Allegro database is never touched.
    public static class IntegrationConfig
    {
        public const string TestDatabaseName = "Allegro_Test";

        public static bool RunDbTests => IsEnabled("RUN_DB_INTEGRATION_TESTS");
        public static bool RunApiTests => IsEnabled("RUN_API_INTEGRATION_TESTS");
        public static bool RunApiWriteTests => IsEnabled("RUN_API_WRITE_TESTS");

        public const string DbSkipReason = "Set RUN_DB_INTEGRATION_TESTS=1 to run repository tests against the Allegro_Test database.";
        public const string ApiSkipReason = "Set RUN_API_INTEGRATION_TESTS=1 to run sandbox API smoke tests.";
        public const string ApiWriteSkipReason = "Set RUN_API_WRITE_TESTS=1 to allow low-risk API writes (one-use delivery address).";

        private static readonly Lazy<IConfigurationRoot> _config = new(Load);

        public static IConfigurationRoot Config => _config.Value;

        public static string TestConnectionString
        {
            get
            {
                var baseConn = Config.GetConnectionString("MyDbContext")
                    ?? throw new InvalidOperationException("ConnectionStrings:MyDbContext missing from ProductsService appsettings.json.");
                return new SqlConnectionStringBuilder(baseConn) { InitialCatalog = TestDatabaseName }.ConnectionString;
            }
        }

        public static GaskaApiCredentials GaskaCredentials =>
            Config.GetSection("GaskaApiCredentials").Get<GaskaApiCredentials>() ?? new();

        public static AllegroApiCredentials AllegroCredentials =>
            Config.GetSection("AllegroApiCredentials").Get<AllegroApiCredentials>() ?? new();

        // Migrations are authored in the Infrastructure project; locate them from the repo root so the
        // test can run dbup against the Allegro_Test database.
        public static string MigrationsDirectory =>
            Path.Combine(RepoRoot(), "AduosSyncServices.Infrastructure", "Migrations");

        private static bool IsEnabled(string name) =>
            Environment.GetEnvironmentVariable(name) is "1" or "true" or "TRUE";

        private static IConfigurationRoot Load()
        {
            var appsettings = Path.Combine(RepoRoot(), "Allegro.Aduos.Gaska.ProductsService", "appsettings.json");
            return new ConfigurationBuilder()
                .AddJsonFile(appsettings, optional: false)
                .Build();
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !dir.EnumerateFiles("*.slnx").Any())
                dir = dir.Parent;

            return dir?.FullName
                ?? throw new InvalidOperationException("Could not locate the repository root (no .slnx found walking up from the test output).");
        }
    }
}
