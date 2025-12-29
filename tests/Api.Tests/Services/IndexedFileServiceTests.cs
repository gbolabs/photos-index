using Api.Services;
using Database;
using Database.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;
using Xunit;

namespace Api.Tests.Services;

public class IndexedFileServiceTests
{
    private readonly PhotosDbContext _dbContext;
    private readonly Mock<ILogger<IndexedFileService>> _mockLogger;
    private readonly IndexedFileService _service;

    public IndexedFileServiceTests()
    {
        // Use in-memory database for testing
        var options = new DbContextOptionsBuilder<PhotosDbContext>()
            .UseInMemoryDatabase(databaseName: "TestIndexedFiles")
            .Options;

        _dbContext = new PhotosDbContext(options);
        _mockLogger = new Mock<ILogger<IndexedFileService>>();
        
        // Create a real configuration with the required values
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new List<KeyValuePair<string, string?>>
            {
                new KeyValuePair<string, string?>("ThumbnailDirectory", "/tmp/thumbnails")
            })
            .Build();

        _service = new IndexedFileService(_dbContext, _mockLogger.Object, configuration);
        
        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var testFiles = new List<IndexedFile>
        {
            new IndexedFile
            {
                Id = Guid.NewGuid(),
                FilePath = "/photos/test1.jpg",
                FileName = "test1.jpg",
                FileHash = "abc123",
                FileSize = 1024,
                Width = 800,
                Height = 600,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                ModifiedAt = DateTime.UtcNow.AddDays(-1),
                IndexedAt = DateTime.UtcNow
            },
            new IndexedFile
            {
                Id = Guid.NewGuid(),
                FilePath = "/photos/test2.jpg",
                FileName = "test2.jpg",
                FileHash = "def456",
                FileSize = 2048,
                Width = 1024,
                Height = 768,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                ModifiedAt = DateTime.UtcNow.AddDays(-2),
                IndexedAt = DateTime.UtcNow
            }
        };

        _dbContext.IndexedFiles.AddRange(testFiles);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetBatchMetadataAsync_ReturnsCorrectFiles()
    {
        // Arrange
        var allFiles = await _dbContext.IndexedFiles.ToListAsync();
        var fileIds = allFiles.Select(f => f.Id).Take(2).ToList();

        // Act
        var result = await _service.GetBatchMetadataAsync(fileIds, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result[0].Id.Should().Be(fileIds[0]);
        result[1].Id.Should().Be(fileIds[1]);
    }

    [Fact]
    public async Task GetBatchMetadataAsync_ReturnsEmptyList_WhenNoFilesFound()
    {
        // Arrange
        var nonExistentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var result = await _service.GetBatchMetadataAsync(nonExistentIds, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(0);
    }

    [Fact]
    public async Task GetBatchMetadataAsync_ReturnsPartialResults_WhenSomeFilesFound()
    {
        // Arrange
        var allFiles = await _dbContext.IndexedFiles.ToListAsync();
        var existingId = allFiles[0].Id;
        var nonExistentId = Guid.NewGuid();
        var fileIds = new List<Guid> { existingId, nonExistentId };

        // Act
        var result = await _service.GetBatchMetadataAsync(fileIds, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);
        result[0].Id.Should().Be(existingId);
    }
}

/// <summary>
/// Tests for search prefix functionality (path:, date:)
/// Regression tests for Issue #135
/// </summary>
public class IndexedFileServiceSearchTests : IDisposable
{
    private readonly PhotosDbContext _dbContext;
    private readonly Mock<ILogger<IndexedFileService>> _mockLogger;
    private readonly IndexedFileService _service;

    public IndexedFileServiceSearchTests()
    {
        // Use unique database name to avoid conflicts
        var options = new DbContextOptionsBuilder<PhotosDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PhotosDbContext(options);
        _mockLogger = new Mock<ILogger<IndexedFileService>>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new List<KeyValuePair<string, string?>>
            {
                new("ThumbnailDirectory", "/tmp/thumbnails")
            })
            .Build();

        _service = new IndexedFileService(_dbContext, _mockLogger.Object, configuration);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        var testFiles = new List<IndexedFile>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FilePath = "/photo/Mobiles/iPhone/2025/12/IMG_001.jpg",
                FileName = "IMG_001.jpg",
                FileHash = "hash1",
                FileSize = 1024,
                Width = 800,
                Height = 600,
                CreatedAt = today.AddHours(10),
                ModifiedAt = today.AddHours(10),
                IndexedAt = DateTime.UtcNow,
                DateTaken = today.AddHours(8)
            },
            new()
            {
                Id = Guid.NewGuid(),
                FilePath = "/photo/Mobiles/iPhone/2025/12/IMG_002.jpg",
                FileName = "IMG_002.jpg",
                FileHash = "hash2",
                FileSize = 2048,
                Width = 1024,
                Height = 768,
                CreatedAt = today.AddHours(14),
                ModifiedAt = today.AddHours(14),
                IndexedAt = DateTime.UtcNow,
                DateTaken = today.AddHours(12)
            },
            new()
            {
                Id = Guid.NewGuid(),
                FilePath = "/photo/Documents/scan.pdf",
                FileName = "scan.pdf",
                FileHash = "hash3",
                FileSize = 512,
                Width = null,
                Height = null,
                CreatedAt = yesterday.AddHours(9),
                ModifiedAt = yesterday.AddHours(9),
                IndexedAt = DateTime.UtcNow,
                DateTaken = null  // No DateTaken, should fall back to CreatedAt
            },
            new()
            {
                Id = Guid.NewGuid(),
                FilePath = "/photo/Mobiles/Samsung/vacation.jpg",
                FileName = "vacation.jpg",
                FileHash = "hash4",
                FileSize = 4096,
                Width = 1920,
                Height = 1080,
                CreatedAt = yesterday.AddHours(15),
                ModifiedAt = yesterday.AddHours(15),
                IndexedAt = DateTime.UtcNow,
                DateTaken = yesterday.AddHours(14)
            }
        };

        _dbContext.IndexedFiles.AddRange(testFiles);
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task QueryAsync_WithPathPrefix_FiltersFilesByPath()
    {
        // Arrange - Search for files in iPhone folder
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = "path:/photo/Mobiles/iPhone"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.All(f => f.FilePath.Contains("/photo/Mobiles/iPhone")).Should().BeTrue();
    }

    [Fact]
    public async Task QueryAsync_WithPathPrefix_IsCaseInsensitive()
    {
        // Arrange - Search with different case
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = "PATH:/PHOTO/MOBILES/IPHONE"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryAsync_WithPathPrefix_ReturnsEmptyWhenNoMatch()
    {
        // Arrange - Search for non-existent path
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = "path:/nonexistent/folder"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_WithTakenPrefix_FiltersFilesByDateTaken()
    {
        // Arrange - Search for today's files by DateTaken
        var today = DateTime.UtcNow.Date;
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = $"taken:{today:yyyy-MM-dd}"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2); // Two files with DateTaken today
        result.Items.All(f => f.DateTaken?.Date == today).Should().BeTrue();
    }

    [Fact]
    public async Task QueryAsync_WithCreatedPrefix_FiltersFilesByCreatedAt()
    {
        // Arrange - Search for yesterday's files by CreatedAt (scan.pdf has CreatedAt yesterday)
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = $"created:{yesterday:yyyy-MM-dd}"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2); // scan.pdf and vacation.jpg have CreatedAt yesterday
    }

    [Fact]
    public async Task QueryAsync_WithModifiedPrefix_FiltersFilesByModifiedAt()
    {
        // Arrange - Search for yesterday's files by ModifiedAt
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = $"modified:{yesterday:yyyy-MM-dd}"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2); // scan.pdf and vacation.jpg have ModifiedAt yesterday
    }

    [Fact]
    public async Task QueryAsync_WithTakenPrefix_ReturnsEmptyWhenNoMatch()
    {
        // Arrange - Search for date with no files
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = "taken:2020-01-01"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_WithInvalidTakenPrefix_ReturnsEmptyResults()
    {
        // Arrange - Search with invalid date
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = "taken:invalid-date"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_WithInvalidCreatedPrefix_ReturnsEmptyResults()
    {
        // Arrange - Search with invalid date
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = "created:not-a-date"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_WithInvalidModifiedPrefix_ReturnsEmptyResults()
    {
        // Arrange - Search with invalid date
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = "modified:xyz"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_WithTakenPrefix_SupportsEuropeanDateFormat()
    {
        // Arrange - Search with dd-MM-yyyy format
        var today = DateTime.UtcNow.Date;
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = $"taken:{today:dd-MM-yyyy}"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2); // Two files with DateTaken today
    }

    [Fact]
    public async Task QueryAsync_WithCreatedPrefix_SupportsEuropeanDateFormat()
    {
        // Arrange - Search with dd-MM-yyyy format
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = $"created:{yesterday:dd-MM-yyyy}"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2); // scan.pdf and vacation.jpg have CreatedAt yesterday
    }

    [Fact]
    public async Task QueryAsync_WithoutPrefix_SearchesByFilename()
    {
        // Arrange - Search by filename (no prefix)
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = "vacation"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items.First().FileName.Should().Contain("vacation");
    }

    [Fact]
    public async Task QueryAsync_WithoutPrefix_FilenameSearchIsCaseInsensitive()
    {
        // Arrange - Search with different case
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = "VACATION"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task QueryAsync_PathPrefixWithSpaces_TrimsAndWorks()
    {
        // Arrange - Search with extra spaces
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = "  path:  /photo/Documents  "
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items.First().FileName.Should().Be("scan.pdf");
    }
}

/// <summary>
/// Tests for hidden files filtering functionality
/// </summary>
public class IndexedFileServiceHiddenFilesTests : IDisposable
{
    private readonly PhotosDbContext _dbContext;
    private readonly Mock<ILogger<IndexedFileService>> _mockLogger;
    private readonly IndexedFileService _service;

    public IndexedFileServiceHiddenFilesTests()
    {
        var options = new DbContextOptionsBuilder<PhotosDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PhotosDbContext(options);
        _mockLogger = new Mock<ILogger<IndexedFileService>>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new List<KeyValuePair<string, string?>>
            {
                new("ThumbnailDirectory", "/tmp/thumbnails")
            })
            .Build();

        _service = new IndexedFileService(_dbContext, _mockLogger.Object, configuration);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var visibleFile1 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/visible1.jpg",
            FileName = "visible1.jpg",
            FileHash = "hash1",
            FileSize = 1024,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            IndexedAt = DateTime.UtcNow,
            IsHidden = false
        };

        var visibleFile2 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/visible2.jpg",
            FileName = "visible2.jpg",
            FileHash = "hash2",
            FileSize = 2048,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            IndexedAt = DateTime.UtcNow,
            IsHidden = false
        };

        var hiddenManually = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/hidden_manual.jpg",
            FileName = "hidden_manual.jpg",
            FileHash = "hash3",
            FileSize = 512,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            IndexedAt = DateTime.UtcNow,
            IsHidden = true,
            HiddenCategory = HiddenCategory.Manual,
            HiddenAt = DateTime.UtcNow
        };

        var hiddenByFolder = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/private/hidden_folder.jpg",
            FileName = "hidden_folder.jpg",
            FileHash = "hash4",
            FileSize = 1024,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            IndexedAt = DateTime.UtcNow,
            IsHidden = true,
            HiddenCategory = HiddenCategory.FolderRule,
            HiddenAt = DateTime.UtcNow,
            HiddenByFolderId = Guid.NewGuid()
        };

        _dbContext.IndexedFiles.AddRange(visibleFile1, visibleFile2, hiddenManually, hiddenByFolder);
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task QueryAsync_ExcludesHiddenFiles_ByDefault()
    {
        // Arrange
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.All(f => !f.IsHidden).Should().BeTrue();
        result.TotalItems.Should().Be(2);
    }

    [Fact]
    public async Task QueryAsync_IncludesHiddenFiles_WhenIncludeHiddenIsTrue()
    {
        // Arrange
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            IncludeHidden = true
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(4);
        result.TotalItems.Should().Be(4);
    }

    [Fact]
    public async Task QueryAsync_ReturnsHiddenPropertiesInDto()
    {
        // Arrange
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            IncludeHidden = true,
            Search = "hidden_manual"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        var hiddenFile = result.Items.First();
        hiddenFile.IsHidden.Should().BeTrue();
        hiddenFile.HiddenCategory.Should().Be("Manual");
        hiddenFile.HiddenAt.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryAsync_WithFilters_StillExcludesHiddenFiles()
    {
        // Arrange
        var query = new FileQueryParameters
        {
            Page = 1,
            PageSize = 50,
            Search = "hidden"
        };

        // Act
        var result = await _service.QueryAsync(query, CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty(); // Both hidden files should be excluded
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsHiddenFileProperties()
    {
        // Arrange
        var hiddenFile = await _dbContext.IndexedFiles
            .FirstAsync(f => f.HiddenCategory == HiddenCategory.Manual);

        // Act
        var result = await _service.GetByIdAsync(hiddenFile.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsHidden.Should().BeTrue();
        result.HiddenCategory.Should().Be("Manual");
        result.HiddenAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetBatchMetadataAsync_ReturnsHiddenFileProperties()
    {
        // Arrange
        var hiddenFile = await _dbContext.IndexedFiles
            .FirstAsync(f => f.HiddenCategory == HiddenCategory.FolderRule);
        var fileIds = new List<Guid> { hiddenFile.Id };

        // Act
        var result = await _service.GetBatchMetadataAsync(fileIds, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().IsHidden.Should().BeTrue();
        result.First().HiddenCategory.Should().Be("FolderRule");
    }
}