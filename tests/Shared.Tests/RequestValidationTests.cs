using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Shared.Requests;
using Xunit;

namespace Shared.Tests;

public class RequestValidationTests
{
    [Fact]
    public void CreateScanDirectoryRequest_ValidPath_ShouldPassValidation()
    {
        // Arrange
        var request = new CreateScanDirectoryRequest { Path = "/photos/family" };

        // Act
        var results = ValidateModel(request);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void CreateScanDirectoryRequest_RelativePath_ShouldFailValidation()
    {
        // Arrange
        var request = new CreateScanDirectoryRequest { Path = "photos/family" };

        // Act
        var results = ValidateModel(request);

        // Assert
        results.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("absolute path");
    }

    [Fact]
    public void CreateScanDirectoryRequest_EmptyPath_ShouldFailValidation()
    {
        // Arrange
        var request = new CreateScanDirectoryRequest { Path = "" };

        // Act
        var results = ValidateModel(request);

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("required"));
    }

    [Theory]
    [InlineData("/photos")]
    [InlineData("/home/user/pictures")]
    [InlineData("/mnt/nas/photos/2024")]
    public void CreateScanDirectoryRequest_ValidAbsolutePaths_ShouldPass(string path)
    {
        // Arrange
        var request = new CreateScanDirectoryRequest { Path = path };

        // Act
        var results = ValidateModel(request);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void UpdateScanDirectoryRequest_NullPath_ShouldPassValidation()
    {
        // Arrange (partial update with only IsEnabled)
        var request = new UpdateScanDirectoryRequest { IsEnabled = false };

        // Act
        var results = ValidateModel(request);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void UpdateScanDirectoryRequest_InvalidPath_ShouldFailValidation()
    {
        // Arrange
        var request = new UpdateScanDirectoryRequest { Path = "relative/path" };

        // Act
        var results = ValidateModel(request);

        // Assert
        results.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("absolute path");
    }

    [Fact]
    public void BatchIngestFilesRequest_EmptyFiles_ShouldFailValidation()
    {
        // Arrange
        var request = new BatchIngestFilesRequest
        {
            ScanDirectoryId = Guid.NewGuid(),
            Files = []
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        results.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("At least one file");
    }

    [Fact]
    public void BatchIngestFilesRequest_ValidFiles_ShouldPassValidation()
    {
        // Arrange
        var request = new BatchIngestFilesRequest
        {
            ScanDirectoryId = Guid.NewGuid(),
            Files =
            [
                new IngestFileItem
                {
                    FilePath = "/photos/test.jpg",
                    FileName = "test.jpg",
                    FileHash = "abc123",
                    FileSize = 1024,
                    ModifiedAt = DateTime.UtcNow
                }
            ]
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void FileQueryParameters_DefaultValues_ShouldBeValid()
    {
        // Arrange
        var request = new FileQueryParameters();

        // Act
        var results = ValidateModel(request);

        // Assert
        results.Should().BeEmpty();
        request.Page.Should().Be(1);
        request.PageSize.Should().Be(50);
        request.SortBy.Should().Be(FileSortBy.IndexedAt);
        request.SortDescending.Should().BeTrue();
    }

    [Fact]
    public void FileQueryParameters_PageSizeTooLarge_ShouldFailValidation()
    {
        // Arrange
        var request = new FileQueryParameters { PageSize = 500 };

        // Act
        var results = ValidateModel(request);

        // Assert
        results.Should().ContainSingle();
    }

    [Fact]
    public void FileQueryParameters_InvalidPage_ShouldFailValidation()
    {
        // Arrange
        var request = new FileQueryParameters { Page = 0 };

        // Act
        var results = ValidateModel(request);

        // Assert
        results.Should().ContainSingle();
    }

    [Fact]
    public void SetOriginalRequest_EmptyGuid_ShouldPassValidation()
    {
        // Note: Empty Guid is technically valid, business logic should handle
        var request = new SetOriginalRequest { FileId = Guid.Empty };

        // Act
        var results = ValidateModel(request);

        // Assert - Empty Guid passes validation, business logic handles
        results.Should().BeEmpty();
    }

    [Fact]
    public void AutoSelectRequest_DefaultValues_ShouldBeValid()
    {
        // Arrange
        var request = new AutoSelectRequest();

        // Act
        var results = ValidateModel(request);

        // Assert
        results.Should().BeEmpty();
        request.Strategy.Should().Be(AutoSelectStrategy.EarliestDate);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
