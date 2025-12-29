using CleanerService;
using CleanerService.Services;
using Shared.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Add OpenTelemetry
builder.AddPhotosIndexTelemetry("photos-index-cleaner");

// Configure options from configuration or environment
builder.Services.Configure<CleanerServiceOptions>(options =>
{
    // Support both appsettings.json and environment variables
    var config = builder.Configuration.GetSection(CleanerServiceOptions.ConfigSection);
    config.Bind(options);

    // Environment variable overrides
    var apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL");
    if (!string.IsNullOrEmpty(apiBaseUrl))
        options.ApiBaseUrl = apiBaseUrl;

    var dryRun = Environment.GetEnvironmentVariable("DRY_RUN");
    if (bool.TryParse(dryRun, out var dryRunValue))
        options.DryRunEnabled = dryRunValue;
});

// Configure HttpClient for API communication
builder.Services.AddHttpClient<IDeleteService, DeleteService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CleanerServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.ApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.UploadTimeoutSeconds);
});

// Register services
builder.Services.AddSingleton<ICleanerStatusService, CleanerStatusService>();
builder.Services.AddSingleton<ISignalRClientService, SignalRClientService>();

// Register hosted services
builder.Services.AddHostedService<SignalRHostedService>();

var host = builder.Build();

// Log startup info
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var options = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CleanerServiceOptions>>().Value;

logger.LogInformation("CleanerService starting...");
logger.LogInformation("  API Base URL: {ApiBaseUrl}", options.ApiBaseUrl);
logger.LogInformation("  Dry Run Mode: {DryRun}", options.DryRunEnabled);
logger.LogInformation("  Hostname: {Hostname}", Environment.MachineName);

host.Run();
