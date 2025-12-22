using Api.Services;
using Database;
using Database.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Dtos;
using Shared.Requests;
using Xunit;

namespace Api.Tests.Services;

public class OriginalSelectionServiceTests : IDisposable
{
    private readonly PhotosDbContext _dbContext;
    private readonly Mock<ILogger<OriginalSelectionService>> _mockLogger;
    private readonly OriginalSelectionService _service;

    public OriginalSelectionServiceTests()
    {
        var options = new DbContextOptionsBuilder<PhotosDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PhotosDbContext(options);
        _mockLogger = new Mock<ILogger<OriginalSelectionService>>();
        _service = new OriginalSelectionService(_dbContext, _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region GetPreferencesAsync Tests

    [Fact]
    public async Task GetPreferencesAsync_ReturnsDefaults_WhenNoPreferencesConfigured()
    {
        // Act
        var preferences = await _service.GetPreferencesAsync(CancellationToken.None);

        // Assert
        preferences.Should().NotBeEmpty();
        preferences.Should().Contain(p => p.PathPrefix == "/photos/");
        preferences.Should().Contain(p => p.PathPrefix == "/backup/");
    }

    [Fact]
    public async Task GetPreferencesAsync_ReturnsConfiguredPreferences()
    {
        // Arrange
        var pref1 = new SelectionPreference
        {
            Id = Guid.NewGuid(),
            PathPrefix = "/custom/",
            Priority = 90,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var pref2 = new SelectionPreference
        {
            Id = Guid.NewGuid(),
            PathPrefix = "/another/",
            Priority = 50,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContext.SelectionPreferences.AddRangeAsync(pref1, pref2);
        await _dbContext.SaveChangesAsync();

        // Act
        var preferences = await _service.GetPreferencesAsync(CancellationToken.None);

        // Assert
        preferences.Should().HaveCount(2);
        preferences[0].PathPrefix.Should().Be("/custom/");
        preferences[0].Priority.Should().Be(90);
        preferences[1].PathPrefix.Should().Be("/another/");
    }

    #endregion

    #region SavePreferencesAsync Tests

    [Fact]
    public async Task SavePreferencesAsync_ReplacesExistingPreferences()
    {
        // Arrange
        var existingPref = new SelectionPreference
        {
            Id = Guid.NewGuid(),
            PathPrefix = "/old/",
            Priority = 50,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContext.SelectionPreferences.AddAsync(existingPref);
        await _dbContext.SaveChangesAsync();

        var request = new SavePreferencesRequest
        {
            Preferences = new List<SelectionPreferenceDto>
            {
                new() { Id = Guid.Empty, PathPrefix = "/new1/", Priority = 80, SortOrder = 0 },
                new() { Id = Guid.Empty, PathPrefix = "/new2/", Priority = 60, SortOrder = 1 }
            }
        };

        // Act
        await _service.SavePreferencesAsync(request, CancellationToken.None);

        // Assert
        var preferences = await _dbContext.SelectionPreferences.ToListAsync();
        preferences.Should().HaveCount(2);
        preferences.Should().NotContain(p => p.PathPrefix == "/old/");
        preferences.Should().Contain(p => p.PathPrefix == "/new1/");
        preferences.Should().Contain(p => p.PathPrefix == "/new2/");
    }

    [Fact]
    public async Task SavePreferencesAsync_ClampsPriorityToValidRange()
    {
        // Arrange
        var request = new SavePreferencesRequest
        {
            Preferences = new List<SelectionPreferenceDto>
            {
                new() { Id = Guid.Empty, PathPrefix = "/test/", Priority = 150, SortOrder = 0 } // Over 100
            }
        };

        // Act
        await _service.SavePreferencesAsync(request, CancellationToken.None);

        // Assert
        var preferences = await _dbContext.SelectionPreferences.ToListAsync();
        preferences[0].Priority.Should().Be(100); // Clamped to max
    }

    #endregion

    #region ResetToDefaultsAsync Tests

    [Fact]
    public async Task ResetToDefaultsAsync_RestoresDefaultPreferences()
    {
        // Arrange
        var customPref = new SelectionPreference
        {
            Id = Guid.NewGuid(),
            PathPrefix = "/custom/",
            Priority = 90,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContext.SelectionPreferences.AddAsync(customPref);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ResetToDefaultsAsync(CancellationToken.None);

        // Assert
        var preferences = await _dbContext.SelectionPreferences.ToListAsync();
        preferences.Should().HaveCount(5); // Default count
        preferences.Should().Contain(p => p.PathPrefix == "/photos/" && p.Priority == 100);
        preferences.Should().Contain(p => p.PathPrefix == "/backup/" && p.Priority == 5);
        preferences.Should().NotContain(p => p.PathPrefix == "/custom/");
    }

    #endregion

    #region RecalculateOriginalsAsync Tests

    [Fact]
    public async Task RecalculateOriginalsAsync_SelectsOriginal_BasedOnPathPriority()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash123",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            Files = new List<IndexedFile>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/backup/photo.jpg",
                    FileName = "photo.jpg",
                    FileHash = "hash123",
                    FileSize = 1000,
                    IndexedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/photos/organized/photo.jpg",
                    FileName = "photo.jpg",
                    FileHash = "hash123",
                    FileSize = 1000,
                    IndexedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                }
            }
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        var request = new RecalculateOriginalsRequest { Scope = "pending" };

        // Act
        var result = await _service.RecalculateOriginalsAsync(request, CancellationToken.None);

        // Assert
        result.Updated.Should().Be(1);
        result.Conflicts.Should().Be(0);

        var updatedGroup = await _dbContext.DuplicateGroups
            .Include(g => g.Files)
            .FirstAsync();

        updatedGroup.Status.Should().Be("auto-selected");
        var original = updatedGroup.Files.FirstOrDefault(f => !f.IsDuplicate);
        original.Should().NotBeNull();
        original!.FilePath.Should().Contain("/photos/"); // Higher priority path
    }

    [Fact]
    public async Task RecalculateOriginalsAsync_MarksConflict_WhenScoresAreTooClose()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash123",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            Files = new List<IndexedFile>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/other/photo1.jpg", // No matching path prefix
                    FileName = "photo1.jpg",
                    FileHash = "hash123",
                    FileSize = 1000,
                    IndexedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/other/photo2.jpg", // Same path prefix, similar structure
                    FileName = "photo2.jpg",
                    FileHash = "hash123",
                    FileSize = 1000,
                    IndexedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                }
            }
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        var request = new RecalculateOriginalsRequest { Scope = "pending" };

        // Act
        var result = await _service.RecalculateOriginalsAsync(request, CancellationToken.None);

        // Assert
        result.Conflicts.Should().Be(1);

        var updatedGroup = await _dbContext.DuplicateGroups.FirstAsync();
        updatedGroup.Status.Should().Be("conflict");
    }

    [Fact]
    public async Task RecalculateOriginalsAsync_PreviewMode_DoesNotApplyChanges()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash123",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            Files = new List<IndexedFile>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/backup/photo.jpg",
                    FileName = "photo.jpg",
                    FileHash = "hash123",
                    FileSize = 1000,
                    IndexedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    IsDuplicate = true
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/photos/photo.jpg",
                    FileName = "photo.jpg",
                    FileHash = "hash123",
                    FileSize = 1000,
                    IndexedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                    IsDuplicate = true
                }
            }
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        var request = new RecalculateOriginalsRequest { Scope = "pending", Preview = true };

        // Act
        var result = await _service.RecalculateOriginalsAsync(request, CancellationToken.None);

        // Assert
        result.Preview.Should().NotBeNull();
        result.Preview.Should().HaveCount(1);

        // Original group should be unchanged
        var unchangedGroup = await _dbContext.DuplicateGroups.FirstAsync();
        unchangedGroup.Status.Should().Be("pending"); // Still pending
    }

    [Fact]
    public async Task RecalculateOriginalsAsync_ScopeAll_ProcessesAllGroups()
    {
        // Arrange
        var pendingGroup = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            Files = new List<IndexedFile>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/photos/file1.jpg",
                    FileName = "file1.jpg",
                    FileHash = "hash1",
                    FileSize = 1000,
                    IndexedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/backup/file1.jpg",
                    FileName = "file1.jpg",
                    FileHash = "hash1",
                    FileSize = 1000,
                    IndexedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                }
            }
        };

        var validatedGroup = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 2,
            TotalSize = 2000,
            Status = "validated",
            CreatedAt = DateTime.UtcNow,
            Files = new List<IndexedFile>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/photos/file2.jpg",
                    FileName = "file2.jpg",
                    FileHash = "hash2",
                    FileSize = 1000,
                    IndexedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    FilePath = "/backup/file2.jpg",
                    FileName = "file2.jpg",
                    FileHash = "hash2",
                    FileSize = 1000,
                    IndexedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                }
            }
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(pendingGroup, validatedGroup);
        await _dbContext.SaveChangesAsync();

        var request = new RecalculateOriginalsRequest { Scope = "all" };

        // Act
        var result = await _service.RecalculateOriginalsAsync(request, CancellationToken.None);

        // Assert
        result.Updated.Should().Be(2); // Both groups processed
    }

    #endregion

    #region CalculateFileScoreAsync Tests

    [Fact]
    public async Task CalculateFileScoreAsync_ReturnsZero_WhenFileNotFound()
    {
        // Act
        var score = await _service.CalculateFileScoreAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        score.Should().Be(0);
    }

    [Fact]
    public async Task CalculateFileScoreAsync_ScoresHigherForPhotosPath()
    {
        // Arrange
        var photosFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/vacation/photo.jpg",
            FileName = "photo.jpg",
            FileHash = "hash123",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        var backupFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/backup/photo.jpg",
            FileName = "photo.jpg",
            FileHash = "hash123",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        await _dbContext.IndexedFiles.AddRangeAsync(photosFile, backupFile);
        await _dbContext.SaveChangesAsync();

        // Act
        var photosScore = await _service.CalculateFileScoreAsync(photosFile.Id, CancellationToken.None);
        var backupScore = await _service.CalculateFileScoreAsync(backupFile.Id, CancellationToken.None);

        // Assert
        photosScore.Should().BeGreaterThan(backupScore);
    }

    [Fact]
    public async Task CalculateFileScoreAsync_ScoresHigherForExifData()
    {
        // Arrange
        var fileWithExif = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/other/photo1.jpg",
            FileName = "photo1.jpg",
            FileHash = "hash123",
            FileSize = 1000,
            Width = 4000,
            Height = 3000,
            IndexedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        var fileWithoutExif = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/other/photo2.jpg",
            FileName = "photo2.jpg",
            FileHash = "hash123",
            FileSize = 1000,
            Width = 0,
            Height = 0,
            IndexedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        await _dbContext.IndexedFiles.AddRangeAsync(fileWithExif, fileWithoutExif);
        await _dbContext.SaveChangesAsync();

        // Act
        var scoreWithExif = await _service.CalculateFileScoreAsync(fileWithExif.Id, CancellationToken.None);
        var scoreWithoutExif = await _service.CalculateFileScoreAsync(fileWithoutExif.Id, CancellationToken.None);

        // Assert
        scoreWithExif.Should().BeGreaterThan(scoreWithoutExif);
    }

    [Fact]
    public async Task CalculateFileScoreAsync_ScoresHigherForDeeperPaths()
    {
        // Arrange
        var deepFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/other/level1/level2/level3/photo.jpg",
            FileName = "photo.jpg",
            FileHash = "hash123",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        var shallowFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/other/photo.jpg",
            FileName = "photo.jpg",
            FileHash = "hash123",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        await _dbContext.IndexedFiles.AddRangeAsync(deepFile, shallowFile);
        await _dbContext.SaveChangesAsync();

        // Act
        var deepScore = await _service.CalculateFileScoreAsync(deepFile.Id, CancellationToken.None);
        var shallowScore = await _service.CalculateFileScoreAsync(shallowFile.Id, CancellationToken.None);

        // Assert
        deepScore.Should().BeGreaterThan(shallowScore);
    }

    #endregion
}
