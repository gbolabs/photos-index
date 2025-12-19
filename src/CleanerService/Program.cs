using CleanerService;
using Shared.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Add OpenTelemetry
builder.AddPhotosIndexTelemetry("photos-index-cleaner");

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
