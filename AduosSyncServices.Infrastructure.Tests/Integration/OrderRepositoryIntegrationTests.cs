using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Data;
using AduosSyncServices.Infrastructure.Repositories;
using AduosSyncServices.Infrastructure.Settings;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace AduosSyncServices.Infrastructure.Tests.Integration
{
    // Exercises the real order stored procedures + the deadlock-fix migration (unique AllegroId index,
    // UPDLOCK/HOLDLOCK upsert) against a dedicated Allegro_Test database. Opt-in via
    // RUN_DB_INTEGRATION_TESTS=1. Uses itemless orders so no Products/FK seeding is needed - the
    // header upsert + round-trip is the deployment-critical DB surface here.
    [Collection("Database")]
    public class OrderRepositoryIntegrationTests
    {
        private readonly TestDatabaseFixture _db;

        public OrderRepositoryIntegrationTests(TestDatabaseFixture db) => _db = db;

        private OrderRepository CreateRepository()
        {
            var context = new DapperContext(_db.ConnectionString);
            var settings = Options.Create(new RepositorySettings
            {
                Company = IntegrationCompany.Gaska,
                Account = AllegroAccount.Aduos
            });
            return new OrderRepository(context, settings);
        }

        private static AllegroOrder ItemlessOrder(string allegroId) => new()
        {
            AllegroId = allegroId,
            Status = AllegroCheckoutFormStatus.READY_FOR_PROCESSING,
            RealizeStatus = AllegroOrderStatus.NEW,
            Amount = 42.50m,
            ClientNickname = "test-nick",
            RecipientFirstName = "Jan",
            RecipientLastName = "Testowy",
            RecipientStreet = "ul. Testowa 1",
            RecipientCity = "Warszawa",
            RecipientPostalCode = "00-001",
            RecipientCountry = "PL",
            DeliveryMethodId = "d1",
            DeliveryMethodName = "Kurier",
            CreatedAt = DateTime.UtcNow,
            Revision = "rev-1",
            PaymentType = AllegroPaymentType.ONLINE,
            Account = AllegroAccount.Aduos,
            IntegrationCompany = IntegrationCompany.Gaska,
            Source = OrderSource.Manual,
            Items = new()
        };

        private async Task DeleteOrder(string allegroId)
        {
            await using var conn = new SqlConnection(_db.ConnectionString);
            await conn.ExecuteAsync("DELETE FROM AllegroOrders WHERE AllegroId = @allegroId", new { allegroId });
        }

        [SkippableFact]
        public async Task SaveAllegroOrder_ThenGetAll_RoundTripsHeader()
        {
            Skip.IfNot(IntegrationConfig.RunDbTests, IntegrationConfig.DbSkipReason);

            var repo = CreateRepository();
            var allegroId = "IT-" + Guid.NewGuid().ToString("N");
            try
            {
                var order = ItemlessOrder(allegroId);
                await repo.SaveAllegroOrder(order);

                Assert.True(order.Id > 0);

                var all = await repo.GetAllOrdersForExternalCompany();
                var saved = Assert.Single(all, o => o.AllegroId == allegroId);
                Assert.Equal(42.50m, saved.Amount);
                Assert.Equal(AllegroAccount.Aduos, saved.Account);
            }
            finally
            {
                await DeleteOrder(allegroId);
            }
        }

        [SkippableFact]
        public async Task SaveAllegroOrder_SameAllegroIdTwice_UpsertsSingleRow()
        {
            Skip.IfNot(IntegrationConfig.RunDbTests, IntegrationConfig.DbSkipReason);

            var repo = CreateRepository();
            var allegroId = "IT-" + Guid.NewGuid().ToString("N");
            try
            {
                var first = ItemlessOrder(allegroId);
                await repo.SaveAllegroOrder(first);
                var firstId = first.Id;

                var second = ItemlessOrder(allegroId);
                second.Amount = 99.99m;
                await repo.SaveAllegroOrder(second);

                // Same AllegroId => same row updated, not a duplicate insert.
                Assert.Equal(firstId, second.Id);

                await using var conn = new SqlConnection(_db.ConnectionString);
                var count = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM AllegroOrders WHERE AllegroId = @allegroId", new { allegroId });
                Assert.Equal(1, count);

                var amount = await conn.ExecuteScalarAsync<decimal>(
                    "SELECT Amount FROM AllegroOrders WHERE AllegroId = @allegroId", new { allegroId });
                Assert.Equal(99.99m, amount);
            }
            finally
            {
                await DeleteOrder(allegroId);
            }
        }

        [SkippableFact]
        public async Task MarkAsOrderedInExternalCompany_SetsSentFlag()
        {
            Skip.IfNot(IntegrationConfig.RunDbTests, IntegrationConfig.DbSkipReason);

            var repo = CreateRepository();
            var allegroId = "IT-" + Guid.NewGuid().ToString("N");
            try
            {
                var order = ItemlessOrder(allegroId);
                await repo.SaveAllegroOrder(order);

                await repo.MarkAsOrderedInExternalCompany(order.Id, externalOrderId: 12345, isDropshipping: true);

                await using var conn = new SqlConnection(_db.ConnectionString);
                var sent = await conn.ExecuteScalarAsync<bool>(
                    "SELECT SentToExternalCompany FROM AllegroOrders WHERE Id = @id", new { id = order.Id });
                Assert.True(sent);
            }
            finally
            {
                await DeleteOrder(allegroId);
            }
        }
    }
}
