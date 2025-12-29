using Api.Consumers;
using Api.Hubs;
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

// Add SignalR
builder.Services.AddSignalR();

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

// Register cleaner services
builder.Services.Configure<CleanerOptions>(builder.Configuration.GetSection(CleanerOptions.ConfigSection));
builder.Services.AddSingleton<CleanerJobService>();
builder.Services.AddScoped<ICleanerJobService>(sp => sp.GetRequiredService<CleanerJobService>());
builder.Services.AddHostedService<CleanerBackgroundService>();
builder.Services.AddHostedService<RetentionBackgroundService>();

var app = builder.Build();

// Log startup info
var buildInfoService = app.Services.GetRequiredService<IBuildInfoService>();
buildInfoService.LogStartupInfo(app.Logger);

// Apply pending database migrations at startup (skip in Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PhotosDbContext>();

    var maxRetries = 10;
    var delay = TimeSpan.FromSeconds(2);

    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            app.Logger.LogInformation("Attempting database migration (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
            db.Database.Migrate();
            app.Logger.LogInformation("Database migration completed successfully");
            break;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            app.Logger.LogWarning(ex, "Database connection failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}s...",
                attempt, maxRetries, delay.TotalSeconds);
            Thread.Sleep(delay);
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 30)); // Exponential backoff, max 30s
        }
    }
}

// Ensure MinIO buckets exist
if (!app.Environment.IsEnvironment("Testing"))
{
    var minioClient = app.Services.GetRequiredService<IMinioClient>();
    var imagesBucket = builder.Configuration["Minio:ImagesBucket"] ?? "images";
    var thumbnailsBucket = builder.Configuration["Minio:ThumbnailsBucket"] ?? "thumbnails";
    var previewsBucket = builder.Configuration["Minio:PreviewsBucket"] ?? "previews";
    var archiveBucket = builder.Configuration["Minio:ArchiveBucket"] ?? "archive";

    foreach (var bucket in new[] { imagesBucket, thumbnailsBucket, previewsBucket, archiveBucket })
    {
        try
        {
            var exists = await minioClient.BucketExistsAsync(new Minio.DataModel.Args.BucketExistsArgs().WithBucket(bucket));
            if (!exists)
            {
                app.Logger.LogInformation("Creating MinIO bucket: {Bucket}", bucket);
                await minioClient.MakeBucketAsync(new Minio.DataModel.Args.MakeBucketArgs().WithBucket(bucket));

                // Make thumbnails and previews buckets publicly readable for Traefik to serve
                if (bucket == thumbnailsBucket || bucket == previewsBucket)
                {
                    var policy = $$"""
                    {
                        "Version": "2012-10-17",
                        "Statement": [{
                            "Effect": "Allow",
                            "Principal": {"AWS": ["*"]},
                            "Action": ["s3:GetObject"],
                            "Resource": ["arn:aws:s3:::{{bucket}}/*"]
                        }]
                    }
                    """;
                    await minioClient.SetPolicyAsync(new Minio.DataModel.Args.SetPolicyArgs()
                        .WithBucket(bucket)
                        .WithPolicy(policy));
                    app.Logger.LogInformation("Set public read policy on bucket: {Bucket}", bucket);
                }

                // Set lifecycle policy for previews bucket (auto-expire after 1 day)
                if (bucket == previewsBucket)
                {
                    var rule = new Minio.DataModel.ILM.LifecycleRule
                    {
                        ID = "auto-expire-previews",
                        Status = "Enabled",
                        Expiration = new Minio.DataModel.ILM.Expiration { Days = 1 }
                    };
                    var lifecycleConfig = new Minio.DataModel.ILM.LifecycleConfiguration([rule]);
                    await minioClient.SetBucketLifecycleAsync(new Minio.DataModel.Args.SetBucketLifecycleArgs()
                        .WithBucket(bucket)
                        .WithLifecycleConfiguration(lifecycleConfig));
                    app.Logger.LogInformation("Set 1-day expiration lifecycle on bucket: {Bucket}", bucket);
                }
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Failed to create/configure MinIO bucket: {Bucket}", bucket);
        }
    }
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

// Map SignalR hubs
app.MapHub<IndexerHub>("/hubs/indexer");
app.MapHub<CleanerHub>("/hubs/cleaner");

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
