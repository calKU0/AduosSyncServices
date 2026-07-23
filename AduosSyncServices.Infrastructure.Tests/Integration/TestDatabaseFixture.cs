using DbUp;
using Xunit;

namespace AduosSyncServices.Infrastructure.Tests.Integration
{
    // Ensures the Allegro_Test database exists and is migrated (dbup) once for the whole DB test
    // collection. Only does anything when RUN_DB_INTEGRATION_TESTS=1; otherwise construction is a
    // no-op so unskipped tests in the same collection don't pay for a DB they won't use.
    public sealed class TestDatabaseFixture
    {
        public bool Ready { get; }

        public string ConnectionString => IntegrationConfig.TestConnectionString;

        public TestDatabaseFixture()
        {
            if (!IntegrationConfig.RunDbTests)
                return;

            EnsureDatabase.For.SqlDatabase(ConnectionString);

            var upgrader = DeployChanges.To
                .SqlDatabase(ConnectionString)
                .WithScriptsFromFileSystem(IntegrationConfig.MigrationsDirectory)
                .WithTransactionPerScript()
                .Build();

            var result = upgrader.PerformUpgrade();
            if (!result.Successful)
                throw new InvalidOperationException("dbup migration of Allegro_Test failed.", result.Error);

            Ready = true;
        }
    }

    [CollectionDefinition("Database")]
    public sealed class DatabaseCollection : ICollectionFixture<TestDatabaseFixture> { }
}
