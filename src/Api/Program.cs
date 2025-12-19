using Database;
using Microsoft.EntityFrameworkCore;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry
builder.AddPhotosIndexTelemetry("photos-index-api");

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add DbContext - connection string will be configured in appsettings.json
builder.Services.AddDbContext<PhotosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Placeholder API endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "Photos Index API" }))
    .WithName("HealthCheck");

app.Run();
