using AduosSyncServices.Contracts.Clients;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.Infrastructure.Clients;
using AduosSyncServices.Infrastructure.Data;
using AduosSyncServices.Infrastructure.Http;
using AduosSyncServices.Infrastructure.Logging;
using AduosSyncServices.Infrastructure.Repositories;
using AduosSyncServices.Infrastructure.Settings;
using Allegro.Aduos.Gaska.ProductsService.Constants;
using Allegro.Aduos.Gaska.ProductsService.Services.Allegro;
using Allegro.Aduos.Gaska.ProductsService.Services.Gaska.Interfaces;
using Allegro.Aduos.Gaska.ProductsService.Services.GaskaApiService;
using Allegro.Aduos.Gaska.ProductsService.Settings;
using DbUp;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net.Http.Headers;

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "AllegroAduosGaskaProductsService";
    })
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        var logsExpirationDays = Convert.ToInt32(configuration["AppSettings:LogsExpirationDays"]);
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

        var connectionString = configuration.GetConnectionString("MyDbContext");
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

        // Bind configuration
        services.Configure<GaskaApiCredentials>(configuration.GetSection("GaskaApiCredentials"));
        services.Configure<AllegroApiCredentials>(configuration.GetSection("AllegroApiCredentials"));
        var appSettingsSection = configuration.GetSection("AppSettings");
        services.Configure<AppSettings>(appSettingsSection);
        services.Configure<PriceSettings>(configuration.GetSection("PriceSettings"));
        services.Configure<AllegroSettings>(configuration.GetSection("AllegroSettings"));

        var appSettings = appSettingsSection.Get<AppSettings>() ?? new AppSettings();
        services.Configure<RepositorySettings>(options =>
        {
            options.Company = ServiceConstants.Company;
            options.Account = ServiceConstants.Account;
            options.Deliveries = appSettings.Deliveries ?? new List<DeliverySettings>();
        });

        // HttpClients
        services.AddTransient<RetryHttpMessageHandler>();

        services.AddHttpClient<AllegroAuthClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["AllegroApiCredentials:AuthBaseUrl"]);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd(configuration["AllegroApiCredentials:UserAgent"]);
        }).AddHttpMessageHandler<RetryHttpMessageHandler>();

        services.AddHttpClient<IAllegroApiClient, AllegroApiClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["AllegroApiCredentials:BaseUrl"]);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.allegro.public.v1+json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd(configuration["AllegroApiCredentials:UserAgent"]);
        }).AddHttpMessageHandler<RetryHttpMessageHandler>();


        services.AddHttpClient<IGaskaApiClient, GaskaApiClient>((sp, client) =>
        {
            var gaskaApi = sp.GetRequiredService<IOptions<GaskaApiCredentials>>().Value;
            client.BaseAddress = new Uri(gaskaApi.BaseUrl);
            GaskaApiAuthHelper.ApplyAuthHeaders(client, gaskaApi);
        }).AddHttpMessageHandler<RetryHttpMessageHandler>();

        // Repositories
        services.AddScoped<ITokenRepository, DbTokenRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IOfferRepository, OfferRepository>();
        services.AddScoped<IImageRepository, ImageRepository>();
        services.AddScoped<IParameterRepository, ParameterRepository>();

        // Services
        services.AddScoped<IGaskaApiService, GaskaApiService>();
        services.AddScoped<IAllegroOfferService, AllegroOfferService>();
        services.AddScoped<IAllegroCategoryService, AllegroCategoryService>();
        services.AddScoped<IAllegroParametersService, AllegroParametersService>();

        // Background worker
        services.AddHostedService<Worker>();

        // Dapper
        services.AddSingleton(sp => new DapperContext(connectionString));

        // Host options
        services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(15));
    })
    .UseSerilog()
    .Build();

host.Run();