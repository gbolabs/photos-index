using Api.Middleware;
using Api.Services;
using Database;
using Microsoft.EntityFrameworkCore;
using Shared.Extensions;

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

// Register application services
builder.Services.AddScoped<IScanDirectoryService, ScanDirectoryService>();
builder.Services.AddScoped<IIndexedFileService, IndexedFileService>();
builder.Services.AddScoped<IDuplicateService, DuplicateService>();
builder.Services.AddSingleton<IBuildInfoService, BuildInfoService>();

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
