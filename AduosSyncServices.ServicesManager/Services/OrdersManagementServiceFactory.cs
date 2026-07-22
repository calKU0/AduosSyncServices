using AduosSyncServices.Contracts.Clients;
using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.Infrastructure.Clients;
using AduosSyncServices.Infrastructure.Data;
using AduosSyncServices.Infrastructure.Http;
using AduosSyncServices.Infrastructure.Repositories;
using AduosSyncServices.Infrastructure.Services;
using AduosSyncServices.Infrastructure.Settings;
using AduosSyncServices.ServicesManager.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Headers;

namespace AduosSyncServices.ServicesManager.Services
{
    public class OrdersManagementContext
    {
        public required IOrderRepository OrderRepository { get; init; }
        public required IProductRepository ProductRepository { get; init; }
        public required IAllegroOrderSyncService SyncService { get; init; }
        public required IGaskaOrderPlacementService PlacementService { get; init; }
        public required List<string> AllegroDeliveryNames { get; init; }
        public required AllegroAccount Account { get; init; }
        public required IntegrationCompany IntegrationCompany { get; init; }
    }

    public class OrdersManagementServiceFactory
    {
        private readonly ConfigService _configService = new();

        public OrdersManagementContext Create(ServiceItem service)
        {
            var config = _configService.LoadAppSettings(service.ExternalConfigPath);
            var connectionString = config.GetConnectionString("MyDbContext")
                ?? throw new InvalidOperationException($"ConnectionStrings:MyDbContext is missing in {service.ExternalConfigPath}.");

            var repoSettings = Options.Create(new RepositorySettings
            {
                Company = IntegrationCompany.Gaska,
                Account = AllegroAccount.Aduos
            });

            var dapperContext = new DapperContext(connectionString);
            var orderRepository = new OrderRepository(dapperContext, repoSettings);
            var productRepository = new ProductRepository(dapperContext, repoSettings);
            var offerRepository = new OfferRepository(dapperContext, repoSettings);

            var allegroCredentials = Options.Create(config.GetSection("AllegroApiCredentials").Get<AllegroApiCredentials>() ?? new AllegroApiCredentials());
            var tokenRepository = new DbTokenRepository(dapperContext, allegroCredentials);

            var allegroAuthHttp = new HttpClient(NewRetryHandler()) { BaseAddress = new Uri(allegroCredentials.Value.AuthBaseUrl) };
            allegroAuthHttp.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
            allegroAuthHttp.DefaultRequestHeaders.UserAgent.ParseAdd(allegroCredentials.Value.UserAgent);
            var allegroAuthClient = new AllegroAuthClient(NullLogger<AllegroAuthClient>.Instance, allegroCredentials, tokenRepository, allegroAuthHttp);

            var allegroApiHttp = new HttpClient(NewRetryHandler()) { BaseAddress = new Uri(allegroCredentials.Value.BaseUrl) };
            allegroApiHttp.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.allegro.public.v1+json"));
            allegroApiHttp.DefaultRequestHeaders.UserAgent.ParseAdd(allegroCredentials.Value.UserAgent);
            var allegroApiClient = new AllegroApiClient(allegroAuthClient, allegroApiHttp);

            var gaskaCredentials = config.GetSection("GaskaApiCredentials").Get<GaskaApiCredentials>() ?? new GaskaApiCredentials();
            var gaskaHttp = new HttpClient(NewRetryHandler()) { BaseAddress = new Uri(gaskaCredentials.BaseUrl) };
            GaskaApiAuthHelper.ApplyAuthHeaders(gaskaHttp, gaskaCredentials);
            var gaskaApiClient = new GaskaApiClient(gaskaHttp);

            var syncService = new AllegroOrderSyncService(NullLogger<AllegroOrderSyncService>.Instance, orderRepository, offerRepository, allegroApiClient, gaskaApiClient);
            var placementService = new GaskaOrderPlacementService(orderRepository, productRepository, gaskaApiClient, allegroApiClient, gaskaCredentials.ProductInterval);
            var allegroDeliveryNames = config.GetSection("AppSettings:AllegroDeliveryNames").Get<List<string>>() ?? new List<string>();

            return new OrdersManagementContext
            {
                OrderRepository = orderRepository,
                ProductRepository = productRepository,
                SyncService = syncService,
                PlacementService = placementService,
                AllegroDeliveryNames = allegroDeliveryNames,
                Account = repoSettings.Value.Account,
                IntegrationCompany = repoSettings.Value.Company
            };
        }

        private static RetryHttpMessageHandler NewRetryHandler() =>
            new(new HttpClientHandler(), NullLogger<RetryHttpMessageHandler>.Instance);
    }
}
