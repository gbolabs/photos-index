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