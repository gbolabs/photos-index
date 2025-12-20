using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Integration.Tests.Fixtures;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;
using Xunit;

namespace Integration.Tests.Api;

/// <summary>
/// Integration tests for ScanDirectories API endpoints.
/// </summary>
public class ScanDirectoriesApiTests : IClassFixture<WebAppFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly WebAppFactory _factory;

    public ScanDirectoriesApiTests(WebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up database after each test
        using var db = _factory.CreateDbContext();
        await TestDataSeeder.ClearAllDataAsync(db);
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyList_WhenNoDirectories()
    {
        // Act
        var response = await _client.GetAsync("/api/scan-directories");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<ScanDirectoryDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_ReturnsDirectories_WithPagination()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        await TestDataSeeder.SeedDirectoryAsync(db, "/photos/dir1");
        await TestDataSeeder.SeedDirectoryAsync(db, "/photos/dir2");
        await TestDataSeeder.SeedDirectoryAsync(db, "/photos/dir3");

        // Act
        var response = await _client.GetAsync("/api/scan-directories?page=1&pageSize=2");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<ScanDirectoryDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.TotalItems.Should().Be(3);
        result.TotalPages.Should().Be(2);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_ReturnsDirectory_WhenExists()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var directory = await TestDataSeeder.SeedDirectoryAsync(db, "/photos/test");

        // Act
        var response = await _client.GetAsync($"/api/scan-directories/{directory.Id}");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<ScanDirectoryDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(directory.Id);
        result.Path.Should().Be("/photos/test");
        result.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/scan-directories/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("NOT_FOUND");
        error.Message.Should().Contain(nonExistentId.ToString());
    }

    [Fact]
    public async Task Create_ReturnsCreatedDirectory()
    {
        // Arrange
        var request = new CreateScanDirectoryRequest
        {
            Path = "/photos/new-test",
            IsEnabled = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/scan-directories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ScanDirectoryDto>();
        result.Should().NotBeNull();
        result!.Path.Should().Be("/photos/new-test");
        result.IsEnabled.Should().BeTrue();
        result.Id.Should().NotBeEmpty();

        // Verify Location header
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(result.Id.ToString());
    }

    [Fact]
    public async Task Create_ReturnsConflict_ForDuplicatePath()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        await TestDataSeeder.SeedDirectoryAsync(db, "/photos/duplicate");

        var request = new CreateScanDirectoryRequest
        {
            Path = "/photos/duplicate",
            IsEnabled = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/scan-directories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("CONFLICT");
        error.Message.Should().Contain("/photos/duplicate");
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_ForInvalidPath()
    {
        // Arrange
        var request = new CreateScanDirectoryRequest
        {
            Path = "relative/path",  // Invalid - must start with /
            IsEnabled = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/scan-directories", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ReturnsUpdatedDirectory()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var directory = await TestDataSeeder.SeedDirectoryAsync(db, "/photos/update-test");

        var updateRequest = new UpdateScanDirectoryRequest
        {
            Path = "/photos/updated-path",
            IsEnabled = false
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/scan-directories/{directory.Id}", updateRequest);

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<ScanDirectoryDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(directory.Id);
        result.Path.Should().Be("/photos/updated-path");
        result.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new UpdateScanDirectoryRequest
        {
            Path = "/photos/updated"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/scan-directories/{nonExistentId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenPathAlreadyExists()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var directory1 = await TestDataSeeder.SeedDirectoryAsync(db, "/photos/dir1");
        var directory2 = await TestDataSeeder.SeedDirectoryAsync(db, "/photos/dir2");

        var updateRequest = new UpdateScanDirectoryRequest
        {
            Path = "/photos/dir2"  // Try to change dir1 to dir2's path
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/scan-directories/{directory1.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_RemovesDirectory()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var directory = await TestDataSeeder.SeedDirectoryAsync(db, "/photos/delete-me");

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/scan-directories/{directory.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var getResponse = await _client.GetAsync($"/api/scan-directories/{directory.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/scan-directories/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TriggerScan_ReturnsAccepted_WhenDirectoryExists()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var directory = await TestDataSeeder.SeedDirectoryAsync(db, "/photos/scan-test");

        // Act
        var response = await _client.PostAsync($"/api/scan-directories/{directory.Id}/trigger-scan", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task TriggerScan_ReturnsNotFound_WhenDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/scan-directories/{nonExistentId}/trigger-scan", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
