using IndexingService;
using Shared.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Add OpenTelemetry
builder.AddPhotosIndexTelemetry("photos-index-indexer");

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
