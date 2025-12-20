using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Integration.Tests.Fixtures;
using Xunit;

namespace Integration.Tests.Api;

/// <summary>
/// Integration tests for health check endpoints.
/// </summary>
public class HealthCheckTests : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client;
    private readonly WebAppFactory _factory;

    public HealthCheckTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.Should().BeSuccessful();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("healthy");
        content.Should().Contain("Photos Index API");
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsJsonContentType()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Api_StartsSuccessfully()
    {
        // This test verifies the entire API infrastructure is working
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.Should().BeSuccessful();

        // Verify database is accessible by querying an endpoint
        var apiResponse = await _client.GetAsync("/api/scan-directories");
        apiResponse.Should().BeSuccessful();
    }
}
