using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Shared.Extensions;

public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing, metrics, and logging to the application.
    /// Configures OTLP exporter with endpoint from environment variable OTEL_EXPORTER_OTLP_ENDPOINT.
    /// </summary>
    /// <param name="builder">The host application builder</param>
    /// <param name="serviceName">The name of the service for telemetry</param>
    /// <returns>The host application builder for chaining</returns>
    public static IHostApplicationBuilder AddPhotosIndexTelemetry(
        this IHostApplicationBuilder builder,
        string serviceName)
    {
        // Allow OTEL_SERVICE_NAME to override the default service name
        var effectiveServiceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
            ?? serviceName;

        // Get OTLP endpoint from environment or use default
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
            ?? "http://localhost:18889";

        // Configure resource attributes
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(
                serviceName: effectiveServiceName,
                serviceVersion: typeof(OpenTelemetryExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0")
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName,
                ["host.name"] = Environment.MachineName
            });

        // Add OpenTelemetry Tracing
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: effectiveServiceName,
                serviceVersion: typeof(OpenTelemetryExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0"))
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(effectiveServiceName)
                    .AddSource("MassTransit") // RabbitMQ message tracing
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.FilterHttpRequestMessage = _ => true;
                        options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                        {
                            activity.SetTag("http.request.method", httpRequestMessage.Method.Method);
                            activity.SetTag("http.request.url", httpRequestMessage.RequestUri?.ToString());
                        };
                        options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                        {
                            activity.SetTag("http.response.status_code", (int)httpResponseMessage.StatusCode);
                        };
                    })
                    .AddEntityFrameworkCoreInstrumentation();

                // Add ASP.NET Core instrumentation if available (for API service)
                try
                {
                    tracing.AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, httpRequest) =>
                        {
                            activity.SetTag("http.request.path", httpRequest.Path);
                            activity.SetTag("http.request.method", httpRequest.Method);
                        };
                        options.EnrichWithHttpResponse = (activity, httpResponse) =>
                        {
                            activity.SetTag("http.response.status_code", httpResponse.StatusCode);
                        };
                    });
                }
                catch
                {
                    // ASP.NET Core instrumentation not available (worker services)
                }

                // Configure OTLP exporter
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                    options.Protocol = OtlpExportProtocol.Grpc;
                });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(effectiveServiceName)
                    .AddMeter("MassTransit") // RabbitMQ message metrics
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation();

                // Add ASP.NET Core metrics if available
                try
                {
                    metrics.AddAspNetCoreInstrumentation();
                }
                catch
                {
                    // ASP.NET Core instrumentation not available (worker services)
                }

                // Configure OTLP exporter
                metrics.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                    options.Protocol = OtlpExportProtocol.Grpc;
                });
            });

        // Add OpenTelemetry Logging
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging
                .SetResourceBuilder(resourceBuilder)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                    options.Protocol = OtlpExportProtocol.Grpc;
                });
        });

        return builder;
    }
}
