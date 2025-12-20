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
/// Integration tests for DuplicateGroups API endpoints.
/// </summary>
public class DuplicateGroupsApiTests : IClassFixture<WebAppFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly WebAppFactory _factory;

    public DuplicateGroupsApiTests(WebAppFactory factory)
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
    public async Task GetAll_ReturnsEmptyList_WhenNoGroups()
    {
        // Act
        var response = await _client.GetAsync("/api/duplicates");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<DuplicateGroupDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task GetAll_ReturnsGroups_WithPagination()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 2);
        await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 3);
        await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 2);

        // Act
        var response = await _client.GetAsync("/api/duplicates?page=1&pageSize=2");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<DuplicateGroupDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.TotalItems.Should().Be(3);
        result.TotalPages.Should().Be(2);
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_ReturnsGroup_WithFiles()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var group = await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 3, fileSize: 2048);

        // Act
        var response = await _client.GetAsync($"/api/duplicates/{group.Id}");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<DuplicateGroupDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(group.Id);
        result.Hash.Should().Be(group.Hash);
        result.FileCount.Should().Be(3);
        result.TotalSize.Should().Be(6144); // 3 * 2048
        result.Files.Should().HaveCount(3);
        result.Files.Should().OnlyContain(f => f.FileHash == group.Hash);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/duplicates/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task SetOriginal_UpdatesOriginalFile()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var group = await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 3);

        // Get the files to select one as original
        var filesResponse = await _client.GetAsync($"/api/duplicates/{group.Id}");
        var groupDto = await filesResponse.Content.ReadFromJsonAsync<DuplicateGroupDto>();
        var fileToMakeOriginal = groupDto!.Files.First();

        var request = new SetOriginalRequest
        {
            FileId = fileToMakeOriginal.Id
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/duplicates/{group.Id}/original", request);

        // Assert
        response.Should().BeSuccessful();

        // Verify the original was set
        var verifyResponse = await _client.GetAsync($"/api/duplicates/{group.Id}");
        var verifyDto = await verifyResponse.Content.ReadFromJsonAsync<DuplicateGroupDto>();
        verifyDto!.OriginalFileId.Should().Be(fileToMakeOriginal.Id);
    }

    [Fact]
    public async Task SetOriginal_ReturnsNotFound_WhenGroupDoesNotExist()
    {
        // Arrange
        var nonExistentGroupId = Guid.NewGuid();
        var request = new SetOriginalRequest
        {
            FileId = Guid.NewGuid()
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/duplicates/{nonExistentGroupId}/original", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AutoSelect_SelectsOriginal_UsingStrategy()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var group = await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 3);

        var request = new AutoSelectRequest
        {
            Strategy = AutoSelectStrategy.EarliestDate
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/duplicates/{group.Id}/auto-select", request);

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadAsStringAsync();
        result.Should().NotBeNullOrEmpty();

        // Verify an original was selected
        var verifyResponse = await _client.GetAsync($"/api/duplicates/{group.Id}");
        var verifyDto = await verifyResponse.Content.ReadFromJsonAsync<DuplicateGroupDto>();
        verifyDto!.OriginalFileId.Should().NotBeNull();
    }

    [Fact]
    public async Task AutoSelect_ReturnsNotFound_WhenGroupDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new AutoSelectRequest
        {
            Strategy = AutoSelectStrategy.EarliestDate
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/duplicates/{nonExistentId}/auto-select", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AutoSelectAll_ProcessesMultipleGroups()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 2);
        await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 3);
        await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 2);

        var request = new AutoSelectRequest
        {
            Strategy = AutoSelectStrategy.EarliestDate
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/duplicates/auto-select-all", request);

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadAsStringAsync();
        result.Should().NotBeNullOrEmpty();

        // The response should indicate how many groups were processed
        result.Should().Contain("groupsProcessed");
    }

    [Fact]
    public async Task GetStatistics_ReturnsCorrectDuplicateStats()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        await TestDataSeeder.SeedFileAsync(db, "normal.jpg", fileSize: 1000);
        await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 3, fileSize: 2000);
        await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 2, fileSize: 1500);

        // Act
        var response = await _client.GetAsync("/api/duplicates/stats");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<FileStatisticsDto>();
        result.Should().NotBeNull();
        result!.DuplicateGroups.Should().Be(2);
        result.DuplicateFiles.Should().Be(5); // 3 + 2
        result.PotentialSavingsBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteNonOriginals_QueuesFilesForDeletion()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var group = await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 3);

        // First, set an original
        var filesResponse = await _client.GetAsync($"/api/duplicates/{group.Id}");
        var groupDto = await filesResponse.Content.ReadFromJsonAsync<DuplicateGroupDto>();
        var originalFile = groupDto!.Files.First();

        await _client.PutAsJsonAsync($"/api/duplicates/{group.Id}/original",
            new SetOriginalRequest { FileId = originalFile.Id });

        // Act
        var response = await _client.DeleteAsync($"/api/duplicates/{group.Id}/non-originals");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadAsStringAsync();
        result.Should().NotBeNullOrEmpty();

        // Verify the response indicates files were queued
        result.Should().Contain("filesQueued");
        result.Should().Contain("2"); // 3 files - 1 original = 2 queued for deletion
    }

    [Fact]
    public async Task DeleteNonOriginals_ReturnsNotFound_WhenGroupDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/duplicates/{nonExistentId}/non-originals");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DuplicateGroupDto_CalculatesPotentialSavings()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var group = await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 4, fileSize: 1000);

        // Act
        var response = await _client.GetAsync($"/api/duplicates/{group.Id}");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<DuplicateGroupDto>();
        result.Should().NotBeNull();
        result!.TotalSize.Should().Be(4000); // 4 * 1000
        result.PotentialSavings.Should().Be(3000); // 4000 - (4000 / 4) = 3000
    }
}
