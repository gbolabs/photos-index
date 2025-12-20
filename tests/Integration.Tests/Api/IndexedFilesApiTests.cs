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
/// Integration tests for IndexedFiles API endpoints.
/// </summary>
public class IndexedFilesApiTests : IClassFixture<WebAppFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly WebAppFactory _factory;

    public IndexedFilesApiTests(WebAppFactory factory)
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
    public async Task Query_ReturnsEmptyList_WhenNoFiles()
    {
        // Act
        var response = await _client.GetAsync("/api/files");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<IndexedFileDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task Query_ReturnsFiles_WithPagination()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        await TestDataSeeder.SeedFileAsync(db, "file1.jpg");
        await TestDataSeeder.SeedFileAsync(db, "file2.jpg");
        await TestDataSeeder.SeedFileAsync(db, "file3.jpg");

        // Act
        var response = await _client.GetAsync("/api/files?page=1&pageSize=2");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<IndexedFileDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.TotalItems.Should().Be(3);
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task Query_FiltersFiles_ByDuplicateStatus()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        await TestDataSeeder.SeedFileAsync(db, "normal.jpg");
        await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 2);

        // Act - Query for duplicates only
        var response = await _client.GetAsync("/api/files?hasDuplicates=true");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<IndexedFileDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(f => f.IsDuplicate);
    }

    [Fact]
    public async Task Query_SearchesFiles_ByFileName()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        await TestDataSeeder.SeedFileAsync(db, "vacation.jpg");
        await TestDataSeeder.SeedFileAsync(db, "vacation2.jpg");
        await TestDataSeeder.SeedFileAsync(db, "work.jpg");

        // Act
        var response = await _client.GetAsync("/api/files?search=vacation");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<IndexedFileDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(f => f.FileName.Contains("vacation"));
    }

    [Fact]
    public async Task GetById_ReturnsFile_WhenExists()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var file = await TestDataSeeder.SeedFileAsync(db, "test.jpg", fileSize: 2048);

        // Act
        var response = await _client.GetAsync($"/api/files/{file.Id}");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<IndexedFileDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(file.Id);
        result.FileName.Should().Be("test.jpg");
        result.FileSize.Should().Be(2048);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/files/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task BatchIngest_CreatesMultipleFiles()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var directory = await TestDataSeeder.SeedDirectoryAsync(db, "/photos/batch");

        var request = new BatchIngestFilesRequest
        {
            ScanDirectoryId = directory.Id,
            Files = new List<IngestFileItem>
            {
                new IngestFileItem
                {
                    FilePath = "/photos/batch/img1.jpg",
                    FileName = "img1.jpg",
                    FileHash = "hash1",
                    FileSize = 1024,
                    ModifiedAt = DateTime.UtcNow
                },
                new IngestFileItem
                {
                    FilePath = "/photos/batch/img2.jpg",
                    FileName = "img2.jpg",
                    FileHash = "hash2",
                    FileSize = 2048,
                    ModifiedAt = DateTime.UtcNow
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/files/batch", request);

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<BatchOperationResponse>();
        result.Should().NotBeNull();
        result!.TotalRequested.Should().Be(2);
        result.Succeeded.Should().Be(2);
        result.Failed.Should().Be(0);
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task BatchIngest_ReturnsBadRequest_WhenNoFiles()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var directory = await TestDataSeeder.SeedDirectoryAsync(db);

        var request = new BatchIngestFilesRequest
        {
            ScanDirectoryId = directory.Id,
            Files = new List<IngestFileItem>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/files/batch", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BatchIngest_DetectsDuplicates()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var directory = await TestDataSeeder.SeedDirectoryAsync(db);

        var duplicateHash = "duplicate-hash-123";
        var request = new BatchIngestFilesRequest
        {
            ScanDirectoryId = directory.Id,
            Files = new List<IngestFileItem>
            {
                new IngestFileItem
                {
                    FilePath = "/photos/dup1.jpg",
                    FileName = "dup1.jpg",
                    FileHash = duplicateHash,
                    FileSize = 1024,
                    ModifiedAt = DateTime.UtcNow
                },
                new IngestFileItem
                {
                    FilePath = "/photos/dup2.jpg",
                    FileName = "dup2.jpg",
                    FileHash = duplicateHash,
                    FileSize = 1024,
                    ModifiedAt = DateTime.UtcNow
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/files/batch", request);

        // Assert
        response.Should().BeSuccessful();

        // Verify duplicate detection - files should be in a duplicate group
        var queryResponse = await _client.GetAsync("/api/files?hasDuplicates=true");
        var result = await queryResponse.Content.ReadFromJsonAsync<PagedResponse<IndexedFileDto>>();
        result!.Items.Should().HaveCount(2);
        // Files with same hash should be assigned to the same duplicate group
        result.Items.Should().OnlyContain(f => f.DuplicateGroupId != null);
        result.Items.Select(f => f.DuplicateGroupId).Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task GetStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        await TestDataSeeder.SeedFileAsync(db, "normal1.jpg", fileSize: 1000);
        await TestDataSeeder.SeedFileAsync(db, "normal2.jpg", fileSize: 2000);
        await TestDataSeeder.SeedDuplicateGroupAsync(db, fileCount: 3, fileSize: 1500);

        // Act
        var response = await _client.GetAsync("/api/files/stats");

        // Assert
        response.Should().BeSuccessful();
        var result = await response.Content.ReadFromJsonAsync<FileStatisticsDto>();
        result.Should().NotBeNull();
        result!.TotalFiles.Should().Be(5); // 2 normal + 3 duplicates
        result.TotalSizeBytes.Should().Be(7500); // 1000 + 2000 + (3 * 1500)
        result.DuplicateGroups.Should().Be(1);
        result.DuplicateFiles.Should().Be(3);
    }

    [Fact]
    public async Task GetThumbnail_ReturnsNotFound_WhenNoThumbnail()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var file = await TestDataSeeder.SeedFileAsync(db, "no-thumb.jpg");

        // Act
        var response = await _client.GetAsync($"/api/files/{file.Id}/thumbnail");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_SoftDeletesFile()
    {
        // Arrange
        using var db = _factory.CreateDbContext();
        var file = await TestDataSeeder.SeedFileAsync(db, "delete-me.jpg");

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/files/{file.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify file is gone from queries (soft deleted)
        var getResponse = await _client.GetAsync($"/api/files/{file.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/files/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
