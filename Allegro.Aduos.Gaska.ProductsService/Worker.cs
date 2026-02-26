using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Infrastructure.Services;
using Allegro.Aduos.Gaska.ProductsService.Services.Gaska.Interfaces;
using Allegro.Aduos.Gaska.ProductsService.Settings;
using Microsoft.Extensions.Options;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppSettings _appSettings;
    private DateTime _lastProductDetailsSyncDate = DateTime.MinValue;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory, IOptions<AppSettings> appSettings)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _appSettings = appSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(_appSettings.FetchIntervalMinutes);

        _logger.LogInformation("Worker started. Interval: {Interval} minutes", _appSettings.FetchIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                await RunSyncCycleAsync(services, stoppingToken);
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

    private async Task RunSyncCycleAsync(IServiceProvider services, CancellationToken ct)
    {
        var gaskaApiService = services.GetRequiredService<IGaskaApiService>();
        var allegroApiClient = services.GetRequiredService<AllegroApiClient>();
        var allegroAuthService = services.GetRequiredService<AllegroAuthService>();

        var offerService = services.GetRequiredService<IAllegroOfferService>();
        var categoryService = services.GetRequiredService<IAllegroCategoryService>();
        var parametersService = services.GetRequiredService<IAllegroParametersService>();

        _logger.LogInformation("=== Starting full synchronization cycle ===");

        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var stepTimes = new Dictionary<string, TimeSpan>();

        try
        {
            async Task MeasureStepAsync(string stepName, Func<Task> action)
            {
                _logger.LogInformation("Starting: {StepName}...", stepName);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await action();
                sw.Stop();
                stepTimes[stepName] = sw.Elapsed;
                _logger.LogInformation($"{stepName} completed in {FormatDuration(sw.Elapsed)}.");
            }

            await MeasureStepAsync("Basic product sync", () => gaskaApiService.SyncProducts());
            await MeasureStepAsync("Allegro offers sync", () => offerService.SyncAllegroOffers());
            await MeasureStepAsync("Allegro offers details sync", () => offerService.SyncAllegroOffersDetails());

            if (_lastProductDetailsSyncDate.Date < DateTime.Today && DateTime.Now.Hour >= 2 && DateTime.Now.Hour <= 10)
            {
                await MeasureStepAsync("Detailed product sync", () => gaskaApiService.SyncProductDetails());
                _lastProductDetailsSyncDate = DateTime.Today;
            }

            await MeasureStepAsync("Allegro categories update", () => categoryService.UpdateAllegroCategories());
            await MeasureStepAsync("Category parameters fetch", () => categoryService.FetchAndSaveCategoryParameters());
            await MeasureStepAsync("Product parameters update", () => parametersService.UpdateParameters());
            await MeasureStepAsync("Offers creation", () => offerService.CreateOffers());
            await MeasureStepAsync("Offers update", () => offerService.UpdateOffers());

            totalStopwatch.Stop();

            _logger.LogInformation("=== Synchronization cycle finished successfully ===");
            _logger.LogInformation("=== Step durations ===");

            foreach (var kv in stepTimes)
            {
                _logger.LogInformation($" - {kv.Key}: {FormatDuration(kv.Value)}");
            }

            _logger.LogInformation($"=== Total time: {FormatDuration(totalStopwatch.Elapsed)} ===");
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