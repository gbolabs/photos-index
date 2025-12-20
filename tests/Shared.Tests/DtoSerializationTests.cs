using System.Text.Json;
using FluentAssertions;
using Shared.Dtos;
using Shared.Responses;
using Xunit;

namespace Shared.Tests;

public class DtoSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void IndexedFileDto_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var dto = new IndexedFileDto
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/test.jpg",
            FileName = "test.jpg",
            FileHash = "abc123def456",
            FileSize = 1024,
            Width = 1920,
            Height = 1080,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            IndexedAt = DateTime.UtcNow,
            ThumbnailPath = "/thumbnails/test.jpg",
            IsDuplicate = true,
            DuplicateGroupId = Guid.NewGuid()
        };

        // Act
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var result = JsonSerializer.Deserialize<IndexedFileDto>(json, JsonOptions);

        // Assert
        result.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public void ScanDirectoryDto_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var dto = new ScanDirectoryDto
        {
            Id = Guid.NewGuid(),
            Path = "/photos/family",
            IsEnabled = true,
            LastScannedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            FileCount = 150
        };

        // Act
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var result = JsonSerializer.Deserialize<ScanDirectoryDto>(json, JsonOptions);

        // Assert
        result.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public void DuplicateGroupDto_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var dto = new DuplicateGroupDto
        {
            Id = Guid.NewGuid(),
            Hash = "abc123",
            FileCount = 3,
            TotalSize = 3072,
            ResolvedAt = null,
            CreatedAt = DateTime.UtcNow,
            OriginalFileId = Guid.NewGuid(),
            Files = new List<IndexedFileDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/photos/a.jpg",
                    FileName = "a.jpg",
                    FileHash = "abc123",
                    FileSize = 1024
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var result = JsonSerializer.Deserialize<DuplicateGroupDto>(json, JsonOptions);

        // Assert
        result.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public void DuplicateGroupDto_PotentialSavings_ShouldCalculateCorrectly()
    {
        // Arrange
        var dto = new DuplicateGroupDto
        {
            Id = Guid.NewGuid(),
            Hash = "abc",
            FileCount = 3,
            TotalSize = 3000,
            CreatedAt = DateTime.UtcNow
        };

        // Act & Assert
        dto.PotentialSavings.Should().Be(2000); // 3000 - (3000/3) = 2000
    }

    [Fact]
    public void DuplicateGroupDto_PotentialSavings_ShouldBeZeroForSingleFile()
    {
        // Arrange
        var dto = new DuplicateGroupDto
        {
            Id = Guid.NewGuid(),
            Hash = "abc",
            FileCount = 1,
            TotalSize = 1000,
            CreatedAt = DateTime.UtcNow
        };

        // Act & Assert
        dto.PotentialSavings.Should().Be(0);
    }

    [Fact]
    public void FileStatisticsDto_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var dto = new FileStatisticsDto
        {
            TotalFiles = 15420,
            TotalSizeBytes = 52428800000,
            DuplicateGroups = 342,
            DuplicateFiles = 1024,
            PotentialSavingsBytes = 2147483648,
            LastIndexedAt = DateTime.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var result = JsonSerializer.Deserialize<FileStatisticsDto>(json, JsonOptions);

        // Assert
        result.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public void PagedResponse_ShouldCalculatePaginationProperties()
    {
        // Arrange
        var response = new PagedResponse<string>
        {
            Items = ["a", "b", "c"],
            Page = 2,
            PageSize = 10,
            TotalItems = 25
        };

        // Act & Assert
        response.TotalPages.Should().Be(3);
        response.HasNextPage.Should().BeTrue();
        response.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void PagedResponse_Empty_ShouldReturnCorrectDefaults()
    {
        // Act
        var response = PagedResponse<string>.Empty();

        // Assert
        response.Items.Should().BeEmpty();
        response.TotalItems.Should().Be(0);
        response.TotalPages.Should().Be(0);
        response.HasNextPage.Should().BeFalse();
        response.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void ApiErrorResponse_NotFound_ShouldHaveCorrectCode()
    {
        // Act
        var response = ApiErrorResponse.NotFound("Item not found");

        // Assert
        response.Message.Should().Be("Item not found");
        response.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public void ApiErrorResponse_BadRequest_ShouldIncludeErrors()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            ["Path"] = ["Path is required"]
        };

        // Act
        var response = ApiErrorResponse.BadRequest("Validation failed", errors);

        // Assert
        response.Code.Should().Be("BAD_REQUEST");
        response.Errors.Should().ContainKey("Path");
    }
}
