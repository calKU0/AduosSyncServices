using Microsoft.Extensions.Options;
using ShopGold.Aduos.Gaska.ProductsService.Services;
using ShopGold.Aduos.Gaska.ProductsService.Settings;
using System.Diagnostics;

namespace ShopGold.Aduos.Gaska.ProductsService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AppSettings _appSettings;

        public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory, IOptions<AppSettings> appSettings)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _appSettings = appSettings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromMinutes(Math.Max(1, _appSettings.FetchIntervalMinutes));
            _logger.LogInformation("Worker started. Interval: {Interval} minutes", interval.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("=== ShopGold synchronization cycle started ===");

                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IShopGoldSyncService>().SyncAsync(stoppingToken);
                    _logger.LogInformation("=== ShopGold synchronization cycle finished in {Duration} ===", sw.Elapsed.ToString(@"hh\:mm\:ss"));
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in ShopGold synchronization cycle.");
                }

                try
                {
                    _logger.LogInformation("Waiting {Delay} before next run...", interval);
                    await Task.Delay(interval, stoppingToken);
                }
                catch (TaskCanceledException) { }
            }

            _logger.LogInformation("Worker stopped.");
        }
    }
}
