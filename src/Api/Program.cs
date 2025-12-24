using Api.Consumers;
using Api.Middleware;
using Api.Services;
using Database;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Minio;
using Shared.Extensions;
using Shared.Storage;

// Application entry point
var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry
builder.AddPhotosIndexTelemetry("photos-index-api");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add DbContext - connection string will be configured in appsettings.json
builder.Services.AddDbContext<PhotosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure MinIO
var minioEndpoint = builder.Configuration["Minio:Endpoint"] ?? "localhost:9000";
var minioAccessKey = builder.Configuration["Minio:AccessKey"] ?? "minioadmin";
var minioSecretKey = builder.Configuration["Minio:SecretKey"] ?? "minioadmin";
var minioUseSsl = builder.Configuration.GetValue("Minio:UseSsl", false);

builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var client = new MinioClient()
        .WithEndpoint(minioEndpoint)
        .WithCredentials(minioAccessKey, minioSecretKey);

    if (minioUseSsl)
        client = client.WithSSL();

    return client.Build();
});
builder.Services.AddSingleton<IObjectStorage, MinioObjectStorage>();

// Configure MassTransit with RabbitMQ
var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitMqUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitMqPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<MetadataExtractedConsumer>();
    x.AddConsumer<ThumbnailGeneratedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitMqHost, "/", h =>
        {
            h.Username(rabbitMqUser);
            h.Password(rabbitMqPass);
        });

        cfg.ConfigureEndpoints(context);
    });
});

// Register application services
builder.Services.AddScoped<IScanDirectoryService, ScanDirectoryService>();
builder.Services.AddScoped<IIndexedFileService, IndexedFileService>();
builder.Services.AddScoped<IDuplicateService, DuplicateService>();
builder.Services.AddScoped<IOriginalSelectionService, OriginalSelectionService>();
builder.Services.AddSingleton<IBuildInfoService, BuildInfoService>();
builder.Services.AddSingleton<IIndexingStatusService, IndexingStatusService>();
builder.Services.AddScoped<IFileIngestService, FileIngestService>();

var app = builder.Build();

// Log startup info
var buildInfoService = app.Services.GetRequiredService<IBuildInfoService>();
buildInfoService.LogStartupInfo(app.Logger);

// Apply pending database migrations at startup (skip in Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PhotosDbContext>();
    db.Database.Migrate();
}

// Add TraceId header to all responses for telemetry correlation
app.UseTraceId();

// Enable Swagger in all environments for API documentation
app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger/{documentName}/swagger.json";
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "Photos Index API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

// Map controllers
app.MapControllers();

// Health check endpoint with version info
app.MapGet("/health", (IBuildInfoService buildInfo) =>
{
    var info = buildInfo.GetBuildInfo();
    return Results.Ok(new
    {
        status = "healthy",
        service = info.ServiceName,
        version = info.Version,
        commit = info.CommitHash,
        uptime = info.Uptime
    });
}).WithName("HealthCheck");

// Full version/build info endpoint
app.MapGet("/api/version", (IBuildInfoService buildInfo) =>
{
    return Results.Ok(buildInfo.GetBuildInfo());
}).WithName("Version");

app.Run();

// Make Program class accessible for testing
public partial class Program { }
