using AduosSyncServices.Infrastructure.Helpers;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.Infrastructure.Data;
using AduosSyncServices.Infrastructure.Logging;
using AduosSyncServices.Infrastructure.Repositories;
using AduosSyncServices.Infrastructure.Settings;
using DbUp;
using Microsoft.Extensions.Options;
using Serilog;
using ShopGold.Aduos.Gaska.ProductsService;
using ShopGold.Aduos.Gaska.ProductsService.Constants;
using ShopGold.Aduos.Gaska.ProductsService.Helpers;
using ShopGold.Aduos.Gaska.ProductsService.Repositories;
using ShopGold.Aduos.Gaska.ProductsService.Services;
using ShopGold.Aduos.Gaska.ProductsService.Settings;
using System.Net;

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = ServiceConstants.ServiceName;
    })
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        var logsExpirationDays = configuration.GetValue("AppSettings:LogsExpirationDays", 14);
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(
                path: Path.Combine(logDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: logsExpirationDays,
                shared: true,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .CreateLogger();

        var connectionString = configuration.GetConnectionString("MyDbContext")
            ?? throw new InvalidOperationException("Connection string 'MyDbContext' not found.");
        EnsureDatabase.For.SqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .LogTo(new SerilogUpgradeLog(Log.Logger))
            .WithScriptsFromFileSystem(Path.Combine(AppContext.BaseDirectory, "Migrations"))
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            Log.Error(result.Error.ToString());
            throw result.Error;
        }

        Log.Information("Database migration completed successfully.");

        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        // Stary zapis kategorii "id, id" wiązanie zamieniłoby na pustą listę.
        services.PostConfigure<AppSettings>(s =>
        {
            s.CategoriesId = ConfigurationLists.ReadIntList(configuration.GetSection("AppSettings:CategoriesId"));

            s.ExcludedCategoryIds = ConfigurationLists.ReadIntList(configuration.GetSection("AppSettings:ExcludedCategoryIds"));
        });
        services.Configure<PriceSettings>(configuration.GetSection("PriceSettings"));
        services.Configure<ShopGoldSettings>(configuration.GetSection("ShopGoldSettings"));
        services.Configure<FtpSettings>(configuration.GetSection("FtpSettings"));
        services.Configure<GpsrSettings>(configuration.GetSection("GpsrSettings"));
        services.Configure<RepositorySettings>(options =>
        {
            options.Company = ServiceConstants.Company;
            options.Account = ServiceConstants.Account;
        });

        services.AddSingleton(sp => new DapperContext(connectionString));
        services.AddSingleton(new ImageHashCache(Path.Combine(AppContext.BaseDirectory, "cache", "image-hashes.tsv")));
        services.AddSingleton<ShopCategoryCache>();

        // Panel ShopGold: sesja administratora w ciasteczkach - jeden kontener na cały czas działania serwisu.
        var cookies = new CookieContainer();
        services.AddHttpClient<IShopGoldPanelClient, ShopGoldPanelClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<ShopGoldSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(30, settings.RequestTimeoutSeconds));
            client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            CookieContainer = cookies,
            UseCookies = true,
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        services.AddScoped<ISyncCategoryRepository, SyncCategoryRepository>();
        services.AddScoped<IShopGoldRepository, ShopGoldRepository>();
        services.AddScoped<IShopGoldFtpClient, ShopGoldFtpClient>();
        services.AddScoped<IShopGoldSyncService, ShopGoldSyncService>();

        services.AddHostedService<Worker>();

        services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(15));
    })
    .UseSerilog()
    .Build();

host.Run();
