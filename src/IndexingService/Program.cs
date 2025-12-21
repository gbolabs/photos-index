using System.Reflection;
using System.Runtime.InteropServices;
using IndexingService;
using IndexingService.ApiClient;
using IndexingService.Services;
using Shared.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.AddPhotosIndexTelemetry("photos-index-indexer");

builder.Services.AddSingleton<IFileScanner, FileScanner>();
builder.Services.AddSingleton<IHashComputer, HashComputer>();
builder.Services.AddSingleton<IMetadataExtractor, MetadataExtractor>();
builder.Services.AddScoped<IIndexingOrchestrator, IndexingOrchestrator>();

var apiBaseUrl = builder.Configuration.GetValue<string>("API_BASE_URL")
    ?? builder.Configuration.GetValue<string>("ApiBaseUrl")
    ?? "http://localhost:5000";
builder.Services.AddHttpClient<IPhotosApiClient, PhotosApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Log startup info
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
var commitHash = Environment.GetEnvironmentVariable("BUILD_COMMIT_HASH") ?? "dev";
var branch = Environment.GetEnvironmentVariable("BUILD_BRANCH") ?? "local";
var buildTime = Environment.GetEnvironmentVariable("BUILD_TIME") ?? "unknown";
var runtime = RuntimeInformation.FrameworkDescription;

logger.LogInformation(
    "Starting photos-index-indexer v{Version} (commit: {CommitHash}, branch: {Branch}, built: {BuildTime})",
    version, commitHash, branch, buildTime);
logger.LogInformation("Runtime: {Runtime}, Environment: {Environment}",
    runtime, builder.Environment.EnvironmentName);

host.Run();
