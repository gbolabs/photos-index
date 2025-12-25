using System.Reflection;
using System.Runtime.InteropServices;
using HeyRed.ImageSharp.Heif.Formats.Avif;
using HeyRed.ImageSharp.Heif.Formats.Heif;
using IndexingService;
using IndexingService.ApiClient;
using IndexingService.Models;
using IndexingService.Services;
using Microsoft.Extensions.Options;
using Shared.Extensions;
using SixLabors.ImageSharp;

// Register HEIC/HEIF and AVIF decoders for Apple photos
Configuration.Default.Configure(new HeifConfigurationModule());
Configuration.Default.Configure(new AvifConfigurationModule());

var builder = Host.CreateApplicationBuilder(args);

builder.AddPhotosIndexTelemetry("photos-index-indexer");

// Configure indexing options
builder.Services.Configure<IndexingOptions>(options =>
{
    options.GenerateThumbnails = builder.Configuration.GetValue<bool?>("GENERATE_THUMBNAILS")
        ?? builder.Configuration.GetValue<bool?>("GenerateThumbnails")
        ?? false; // Disabled by default for distributed processing
    options.ExtractMetadata = builder.Configuration.GetValue<bool?>("EXTRACT_METADATA")
        ?? builder.Configuration.GetValue<bool?>("ExtractMetadata")
        ?? false; // Disabled by default for distributed processing
    options.BatchSize = builder.Configuration.GetValue<int?>("BATCH_SIZE")
        ?? builder.Configuration.GetValue<int?>("BatchSize")
        ?? 100;
    options.MaxParallelism = builder.Configuration.GetValue<int?>("MAX_PARALLELISM")
        ?? builder.Configuration.GetValue<int?>("MaxParallelism")
        ?? 4;
});

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
