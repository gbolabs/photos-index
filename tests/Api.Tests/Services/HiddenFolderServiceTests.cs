using Api.Services;
using Database;
using Database.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Dtos;
using Xunit;

namespace Api.Tests.Services;

public class HiddenFolderServiceTests : IDisposable
{
    private readonly PhotosDbContext _dbContext;
    private readonly Mock<ILogger<HiddenFolderService>> _mockLogger;
    private readonly HiddenFolderService _service;

    public HiddenFolderServiceTests()
    {
        var options = new DbContextOptionsBuilder<PhotosDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PhotosDbContext(options);
        _mockLogger = new Mock<ILogger<HiddenFolderService>>();
        _service = new HiddenFolderService(_dbContext, _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoHiddenFolders()
    {
        // Act
        var result = await _service.GetAllAsync(CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllHiddenFolders_WithAffectedFileCounts()
    {
        // Arrange
        var folder1 = new HiddenFolder
        {
            Id = Guid.NewGuid(),
            FolderPath = "/photos/private",
            Description = "Private photos",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var folder2 = new HiddenFolder
        {
            Id = Guid.NewGuid(),
            FolderPath = "/photos/temp",
            Description = "Temporary files",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.HiddenFolders.AddRange(folder1, folder2);

        // Add files hidden by folder1
        _dbContext.IndexedFiles.AddRange(
            new IndexedFile
            {
                Id = Guid.NewGuid(),
                FilePath = "/photos/private/file1.jpg",
                FileName = "file1.jpg",
                FileHash = "hash1",
                IsHidden = true,
                HiddenByFolderId = folder1.Id
            },
            new IndexedFile
            {
                Id = Guid.NewGuid(),
                FilePath = "/photos/private/file2.jpg",
                FileName = "file2.jpg",
                FileHash = "hash2",
                IsHidden = true,
                HiddenByFolderId = folder1.Id
            }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAllAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        var privateFolder = result.First(f => f.FolderPath == "/photos/private");
        privateFolder.AffectedFileCount.Should().Be(2);
        privateFolder.Description.Should().Be("Private photos");

        var tempFolder = result.First(f => f.FolderPath == "/photos/temp");
        tempFolder.AffectedFileCount.Should().Be(0);
    }

    #endregion

    #region GetFolderPathsAsync Tests

    [Fact]
    public async Task GetFolderPathsAsync_ReturnsDistinctFolderPaths()
    {
        // Arrange
        _dbContext.IndexedFiles.AddRange(
            new IndexedFile { Id = Guid.NewGuid(), FilePath = "/photos/2023/01/file1.jpg", FileName = "file1.jpg", FileHash = "h1" },
            new IndexedFile { Id = Guid.NewGuid(), FilePath = "/photos/2023/01/file2.jpg", FileName = "file2.jpg", FileHash = "h2" },
            new IndexedFile { Id = Guid.NewGuid(), FilePath = "/photos/2023/02/file3.jpg", FileName = "file3.jpg", FileHash = "h3" },
            new IndexedFile { Id = Guid.NewGuid(), FilePath = "/documents/file4.jpg", FileName = "file4.jpg", FileHash = "h4" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetFolderPathsAsync(null, CancellationToken.None);

        // Assert
        result.Should().HaveCountGreaterOrEqualTo(3);
        result.Select(r => r.Path).Should().Contain("/photos/2023/01");
        result.Select(r => r.Path).Should().Contain("/photos/2023/02");
        result.Select(r => r.Path).Should().Contain("/documents");
    }

    [Fact]
    public async Task GetFolderPathsAsync_FiltersWithSearchTerm()
    {
        // Arrange
        _dbContext.IndexedFiles.AddRange(
            new IndexedFile { Id = Guid.NewGuid(), FilePath = "/photos/vacation/file1.jpg", FileName = "file1.jpg", FileHash = "h1" },
            new IndexedFile { Id = Guid.NewGuid(), FilePath = "/documents/work/file2.pdf", FileName = "file2.pdf", FileHash = "h2" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetFolderPathsAsync("vacation", CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Path.Should().Contain("vacation");
    }

    [Fact]
    public async Task GetFolderPathsAsync_SearchIsCaseInsensitive()
    {
        // Arrange
        _dbContext.IndexedFiles.Add(
            new IndexedFile { Id = Guid.NewGuid(), FilePath = "/photos/VACATION/file1.jpg", FileName = "file1.jpg", FileHash = "h1" }
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetFolderPathsAsync("vacation", CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CreatesHiddenFolder_WithNoMatchingFiles()
    {
        // Arrange
        var request = new CreateHiddenFolderRequest
        {
            FolderPath = "/photos/private",
            Description = "Private photos"
        };

        // Act
        var result = await _service.CreateAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FolderPath.Should().Be("/photos/private");
        result.Description.Should().Be("Private photos");
        result.AffectedFileCount.Should().Be(0);

        var dbFolder = await _dbContext.HiddenFolders.FirstOrDefaultAsync();
        dbFolder.Should().NotBeNull();
        dbFolder!.FolderPath.Should().Be("/photos/private");
    }

    [Fact]
    public async Task CreateAsync_HidesMatchingFiles()
    {
        // Arrange
        var file1 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/private/file1.jpg",
            FileName = "file1.jpg",
            FileHash = "hash1"
        };
        var file2 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/private/subfolder/file2.jpg",
            FileName = "file2.jpg",
            FileHash = "hash2"
        };
        var file3 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/public/file3.jpg",
            FileName = "file3.jpg",
            FileHash = "hash3"
        };
        _dbContext.IndexedFiles.AddRange(file1, file2, file3);
        await _dbContext.SaveChangesAsync();

        var request = new CreateHiddenFolderRequest { FolderPath = "/photos/private" };

        // Act
        var result = await _service.CreateAsync(request, CancellationToken.None);

        // Assert
        result.AffectedFileCount.Should().Be(2);

        var hiddenFile1 = await _dbContext.IndexedFiles.FindAsync(file1.Id);
        hiddenFile1!.IsHidden.Should().BeTrue();
        hiddenFile1.HiddenCategory.Should().Be(HiddenCategory.FolderRule);
        hiddenFile1.HiddenAt.Should().NotBeNull();
        hiddenFile1.HiddenByFolderId.Should().Be(result.Id);

        var publicFile = await _dbContext.IndexedFiles.FindAsync(file3.Id);
        publicFile!.IsHidden.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_NormalizesFolderPath_RemovingTrailingSlash()
    {
        // Arrange
        var request = new CreateHiddenFolderRequest { FolderPath = "/photos/private/" };

        // Act
        var result = await _service.CreateAsync(request, CancellationToken.None);

        // Assert
        result.FolderPath.Should().Be("/photos/private");
    }

    [Fact]
    public async Task CreateAsync_ThrowsException_WhenRuleAlreadyExists()
    {
        // Arrange
        _dbContext.HiddenFolders.Add(new HiddenFolder
        {
            Id = Guid.NewGuid(),
            FolderPath = "/photos/private",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var request = new CreateHiddenFolderRequest { FolderPath = "/photos/private" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_DoesNotHideAlreadyHiddenFiles()
    {
        // Arrange
        var file = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/private/file1.jpg",
            FileName = "file1.jpg",
            FileHash = "hash1",
            IsHidden = true,
            HiddenCategory = HiddenCategory.Manual,
            HiddenAt = DateTime.UtcNow.AddDays(-1)
        };
        _dbContext.IndexedFiles.Add(file);
        await _dbContext.SaveChangesAsync();

        var request = new CreateHiddenFolderRequest { FolderPath = "/photos/private" };

        // Act
        var result = await _service.CreateAsync(request, CancellationToken.None);

        // Assert
        result.AffectedFileCount.Should().Be(0);

        // Verify file still has original hidden status
        var dbFile = await _dbContext.IndexedFiles.FindAsync(file.Id);
        dbFile!.HiddenCategory.Should().Be(HiddenCategory.Manual);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenFolderNotFound()
    {
        // Act
        var result = await _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DeletesFolderAndUnhidesFiles()
    {
        // Arrange
        var folderId = Guid.NewGuid();
        var folder = new HiddenFolder
        {
            Id = folderId,
            FolderPath = "/photos/private",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.HiddenFolders.Add(folder);

        var file1 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/private/file1.jpg",
            FileName = "file1.jpg",
            FileHash = "hash1",
            IsHidden = true,
            HiddenCategory = HiddenCategory.FolderRule,
            HiddenAt = DateTime.UtcNow,
            HiddenByFolderId = folderId
        };
        var file2 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/private/file2.jpg",
            FileName = "file2.jpg",
            FileHash = "hash2",
            IsHidden = true,
            HiddenCategory = HiddenCategory.FolderRule,
            HiddenAt = DateTime.UtcNow,
            HiddenByFolderId = folderId
        };
        _dbContext.IndexedFiles.AddRange(file1, file2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.DeleteAsync(folderId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        var dbFolder = await _dbContext.HiddenFolders.FindAsync(folderId);
        dbFolder.Should().BeNull();

        var unhiddenFile1 = await _dbContext.IndexedFiles.FindAsync(file1.Id);
        unhiddenFile1!.IsHidden.Should().BeFalse();
        unhiddenFile1.HiddenCategory.Should().BeNull();
        unhiddenFile1.HiddenAt.Should().BeNull();
        unhiddenFile1.HiddenByFolderId.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_OnlyUnhidesFilesFromThisFolder()
    {
        // Arrange
        var folder1Id = Guid.NewGuid();
        var folder2Id = Guid.NewGuid();
        _dbContext.HiddenFolders.AddRange(
            new HiddenFolder { Id = folder1Id, FolderPath = "/photos/private", CreatedAt = DateTime.UtcNow },
            new HiddenFolder { Id = folder2Id, FolderPath = "/photos/temp", CreatedAt = DateTime.UtcNow }
        );

        var file1 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/private/file1.jpg",
            FileName = "file1.jpg",
            FileHash = "hash1",
            IsHidden = true,
            HiddenByFolderId = folder1Id
        };
        var file2 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/temp/file2.jpg",
            FileName = "file2.jpg",
            FileHash = "hash2",
            IsHidden = true,
            HiddenByFolderId = folder2Id
        };
        _dbContext.IndexedFiles.AddRange(file1, file2);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.DeleteAsync(folder1Id, CancellationToken.None);

        // Assert
        var unhiddenFile = await _dbContext.IndexedFiles.FindAsync(file1.Id);
        unhiddenFile!.IsHidden.Should().BeFalse();

        var stillHiddenFile = await _dbContext.IndexedFiles.FindAsync(file2.Id);
        stillHiddenFile!.IsHidden.Should().BeTrue();
    }

    #endregion

    #region HideFilesAsync Tests

    [Fact]
    public async Task HideFilesAsync_HidesFiles_WithManualCategory()
    {
        // Arrange
        var file1 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/file1.jpg",
            FileName = "file1.jpg",
            FileHash = "hash1"
        };
        var file2 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/file2.jpg",
            FileName = "file2.jpg",
            FileHash = "hash2"
        };
        _dbContext.IndexedFiles.AddRange(file1, file2);
        await _dbContext.SaveChangesAsync();

        var request = new HideFilesRequest { FileIds = [file1.Id, file2.Id] };

        // Act
        var result = await _service.HideFilesAsync(request, CancellationToken.None);

        // Assert
        result.Should().Be(2);

        var hiddenFile1 = await _dbContext.IndexedFiles.FindAsync(file1.Id);
        hiddenFile1!.IsHidden.Should().BeTrue();
        hiddenFile1.HiddenCategory.Should().Be(HiddenCategory.Manual);
        hiddenFile1.HiddenAt.Should().NotBeNull();
        hiddenFile1.HiddenByFolderId.Should().BeNull();
    }

    [Fact]
    public async Task HideFilesAsync_DoesNotHideAlreadyHiddenFiles()
    {
        // Arrange
        var file = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/file1.jpg",
            FileName = "file1.jpg",
            FileHash = "hash1",
            IsHidden = true,
            HiddenCategory = HiddenCategory.FolderRule
        };
        _dbContext.IndexedFiles.Add(file);
        await _dbContext.SaveChangesAsync();

        var request = new HideFilesRequest { FileIds = [file.Id] };

        // Act
        var result = await _service.HideFilesAsync(request, CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task HideFilesAsync_ReturnsZero_WhenNoFilesFound()
    {
        // Arrange
        var request = new HideFilesRequest { FileIds = [Guid.NewGuid(), Guid.NewGuid()] };

        // Act
        var result = await _service.HideFilesAsync(request, CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region UnhideFilesAsync Tests

    [Fact]
    public async Task UnhideFilesAsync_UnhidesFiles()
    {
        // Arrange
        var file1 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/file1.jpg",
            FileName = "file1.jpg",
            FileHash = "hash1",
            IsHidden = true,
            HiddenCategory = HiddenCategory.Manual,
            HiddenAt = DateTime.UtcNow
        };
        var file2 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/file2.jpg",
            FileName = "file2.jpg",
            FileHash = "hash2",
            IsHidden = true,
            HiddenCategory = HiddenCategory.FolderRule,
            HiddenAt = DateTime.UtcNow,
            HiddenByFolderId = Guid.NewGuid()
        };
        _dbContext.IndexedFiles.AddRange(file1, file2);
        await _dbContext.SaveChangesAsync();

        var request = new HideFilesRequest { FileIds = [file1.Id, file2.Id] };

        // Act
        var result = await _service.UnhideFilesAsync(request, CancellationToken.None);

        // Assert
        result.Should().Be(2);

        var unhiddenFile1 = await _dbContext.IndexedFiles.FindAsync(file1.Id);
        unhiddenFile1!.IsHidden.Should().BeFalse();
        unhiddenFile1.HiddenCategory.Should().BeNull();
        unhiddenFile1.HiddenAt.Should().BeNull();
        unhiddenFile1.HiddenByFolderId.Should().BeNull();
    }

    [Fact]
    public async Task UnhideFilesAsync_DoesNotAffectNonHiddenFiles()
    {
        // Arrange
        var file = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/file1.jpg",
            FileName = "file1.jpg",
            FileHash = "hash1",
            IsHidden = false
        };
        _dbContext.IndexedFiles.Add(file);
        await _dbContext.SaveChangesAsync();

        var request = new HideFilesRequest { FileIds = [file.Id] };

        // Act
        var result = await _service.UnhideFilesAsync(request, CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    #endregion
}
