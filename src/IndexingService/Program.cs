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

var apiBaseUrl = builder.Configuration.GetValue<string>("ApiBaseUrl") ?? "http://localhost:5000";
builder.Services.AddHttpClient<IPhotosApiClient, PhotosApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
