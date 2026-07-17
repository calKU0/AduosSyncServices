using AduosSyncServices.Contracts.Interfaces;
using Allegro.Aduos.Gaska.OrdersService.Settings;
using Microsoft.Extensions.Options;

namespace Allegro.Aduos.Gaska.OrdersService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly AppSettings _appSettings;
        private readonly IServiceProvider _serviceProvider;

        public Worker(ILogger<Worker> logger, IOptions<AppSettings> appSettings, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _appSettings = appSettings.Value;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromMinutes(_appSettings.FetchIntervalMinutes);

            _logger.LogInformation("Worker started. Interval: {Interval} minutes", _appSettings.FetchIntervalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunSyncCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in synchronization loop.");
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

        private async Task RunSyncCycleAsync(CancellationToken ct)
        {
            _logger.LogInformation("=== Starting full synchronization cycle ===");

            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            var stepTimes = new Dictionary<string, TimeSpan>();

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IAllegroOrderSyncService>();

                async Task MeasureStepAsync(string stepName, Func<Task> action)
                {
                    _logger.LogInformation($"Starting {stepName}...");
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    await action();
                    sw.Stop();
                    stepTimes[stepName] = sw.Elapsed;
                    _logger.LogInformation($"{stepName} completed in {FormatDuration(sw.Elapsed)}.");
                }

                await MeasureStepAsync("Sync orders from Allegro", () => orderService.SyncOrdersFromAllegro(_appSettings.AllegroDeliveryNames));
                await MeasureStepAsync("Update Gaska order info", () => orderService.UpdateOrderGaskaInfo());
                await MeasureStepAsync("Update orders in Allegro", () => orderService.UpdateOrdersInAllegro());

                totalSw.Stop();

                _logger.LogInformation("=== Synchronization cycle finished successfully ===");
                _logger.LogInformation("=== Step durations ===");

                foreach (var kv in stepTimes)
                {
                    _logger.LogInformation($" - {kv.Key}: {FormatDuration(kv.Value)}");
                }

                _logger.LogInformation($"=== Total time: {FormatDuration(totalSw.Elapsed)} ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during synchronization cycle.");
            }

            string FormatDuration(TimeSpan timeSpan)
            {
                return $"{(int)timeSpan.TotalMinutes:D2}m {timeSpan.Seconds:D2}s";
            }
        }
    }
}