using HeyRed.ImageSharp.Heif;
using MassTransit;
using MetadataService;
using Minio;
using Shared.Extensions;
using Shared.Storage;
using SixLabors.ImageSharp;

// Register HEIF/HEIC decoder with ImageSharp
Configuration.Default.Configure(new HeifConfigurationModule());

var builder = Host.CreateApplicationBuilder(args);

// Add OpenTelemetry
builder.AddPhotosIndexTelemetry("photos-index-metadata-service");

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
    x.AddConsumer<FileDiscoveredConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitMqHost, "/", h =>
        {
            h.Username(rabbitMqUser);
            h.Password(rabbitMqPass);
        });

        // Use unique queue name so metadata-service gets its own copy of FileDiscoveredMessage
        cfg.ReceiveEndpoint("metadata-file-discovered", e =>
        {
            e.ConfigureConsumer<FileDiscoveredConsumer>(context);
        });
    });
});

var host = builder.Build();
host.Run();
