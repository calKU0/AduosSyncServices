using AduosSyncServices.Contracts.DTOs.GaskaApi;
using AduosSyncServices.Infrastructure.Clients;
using AduosSyncServices.Infrastructure.Http;
using Xunit;

namespace AduosSyncServices.Infrastructure.Tests.Integration
{
    // Smoke tests against the real Gąska test API. Gąska uses stateless signature auth, so these run
    // headlessly. Reads are opt-in via RUN_API_INTEGRATION_TESTS=1; the one write (a one-use delivery
    // address) additionally needs RUN_API_WRITE_TESTS=1.
    //
    // Deliberately NOT automated: CreateOrder (a supplier order is a real fulfilment/purchase) and
    // Allegro order-status writes. Those consequential paths are covered by the mocked unit tests in
    // GaskaOrderPlacementServicePartialTests instead.
    public class GaskaApiClientIntegrationTests
    {
        private static GaskaApiClient CreateClient()
        {
            var creds = IntegrationConfig.GaskaCredentials;
            var http = new HttpClient(new RetryHttpMessageHandler(new HttpClientHandler()))
            {
                BaseAddress = new Uri(creds.BaseUrl),
                Timeout = TimeSpan.FromSeconds(60)
            };
            GaskaApiAuthHelper.ApplyAuthHeaders(http, creds);
            return new GaskaApiClient(http);
        }

        private static int FirstConfiguredCategory()
        {
            var raw = IntegrationConfig.Config["AppSettings:CategoriesId"] ?? "";
            var first = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            return int.TryParse(first, out var id) ? id : 0;
        }

        [SkippableFact]
        public async Task GetProducts_ReturnsSuccessfulResponse()
        {
            Skip.IfNot(IntegrationConfig.RunApiTests, IntegrationConfig.ApiSkipReason);

            var response = await CreateClient().GetProducts(new GaskaGetProductsRequest
            {
                CategoryId = FirstConfiguredCategory(),
                PageSize = 5,
                Page = 1,
                Language = "pl"
            });

            Assert.NotNull(response);
            Assert.Equal(0, response.Result);
        }

        [SkippableFact]
        public async Task GetProduct_ForFirstListedProduct_ReturnsMatchingId()
        {
            Skip.IfNot(IntegrationConfig.RunApiTests, IntegrationConfig.ApiSkipReason);

            var client = CreateClient();
            var list = await client.GetProducts(new GaskaGetProductsRequest
            {
                CategoryId = FirstConfiguredCategory(),
                PageSize = 5,
                Page = 1,
                Language = "pl"
            });
            Skip.If(list.Products is null || list.Products.Count == 0, "No products returned for the configured category.");

            var id = list.Products![0].Id;
            var product = await client.GetProduct(id, "pl");

            Assert.NotNull(product);
            Assert.Equal(0, product.Result);
            Assert.Equal(id, product.Product.Id);
        }

        [SkippableFact]
        public async Task CreateDeliveryAddress_OneUse_ReturnsAddressId()
        {
            Skip.IfNot(IntegrationConfig.RunApiTests, IntegrationConfig.ApiSkipReason);
            Skip.IfNot(IntegrationConfig.RunApiWriteTests, IntegrationConfig.ApiWriteSkipReason);

            var response = await CreateClient().CreateDeliveryAddress(new GaskaCreateDeliveryAddressRequest
            {
                Name1 = "Test Integracyjny",
                Street = "ul. Testowa 1",
                City = "Warszawa",
                PostalCode = "00-001",
                Country = "PL",
                Phone = "500600700",
                Email = "kontakt@agro-aduos.pl",
                OneUse = true
            });

            Assert.NotNull(response);
            Assert.Equal(0, response.Result);
            Assert.True(response.AddressId > 0);
        }
    }
}
