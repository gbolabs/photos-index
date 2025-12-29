using Api.Services;
using Database;
using Database.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Requests;
using Xunit;

namespace Api.Tests.Services;

public class DuplicateServiceTests : IDisposable
{
    private readonly PhotosDbContext _dbContext;
    private readonly Mock<ILogger<DuplicateService>> _mockLogger;
    private readonly DuplicateService _service;

    public DuplicateServiceTests()
    {
        var options = new DbContextOptionsBuilder<PhotosDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PhotosDbContext(options);
        _mockLogger = new Mock<ILogger<DuplicateService>>();
        _service = new DuplicateService(_dbContext, _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region GetGroupsAsync Tests

    [Fact]
    public async Task GetGroupsAsync_WithStatusFilter_ReturnsFilteredGroups()
    {
        // Arrange
        var pendingGroup = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var validatedGroup = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 3,
            TotalSize = 3000,
            Status = "validated",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(pendingGroup, validatedGroup);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetGroupsAsync(1, 20, "pending", CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items.First().Status.Should().Be("pending");
        result.TotalItems.Should().Be(1);
    }

    [Fact]
    public async Task GetGroupsAsync_WithoutStatusFilter_ReturnsAllGroups()
    {
        // Arrange
        var pendingGroup = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var validatedGroup = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 3,
            TotalSize = 3000,
            Status = "validated",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(pendingGroup, validatedGroup);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetGroupsAsync(1, 20, null, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalItems.Should().Be(2);
    }

    [Fact]
    public async Task GetGroupsAsync_IncludesValidationFields()
    {
        // Arrange
        var validatedAt = DateTime.UtcNow.AddHours(-1);
        var keptFileId = Guid.NewGuid();
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "validated",
            ValidatedAt = validatedAt,
            KeptFileId = keptFileId,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetGroupsAsync(1, 20, null, CancellationToken.None);

        // Assert
        var dto = result.Items.First();
        dto.Status.Should().Be("validated");
        dto.ValidatedAt.Should().BeCloseTo(validatedAt, TimeSpan.FromSeconds(1));
        dto.KeptFileId.Should().Be(keptFileId);
    }

    #endregion

    #region ValidateDuplicatesAsync Tests

    [Fact]
    public async Task ValidateDuplicatesAsync_ValidatesSpecifiedGroups()
    {
        // Arrange
        var group1 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var group2 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 3,
            TotalSize = 3000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var group3 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash3",
            FileCount = 2,
            TotalSize = 1500,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(group1, group2, group3);
        await _dbContext.SaveChangesAsync();

        var request = new ValidateDuplicatesRequest
        {
            GroupIds = new List<Guid> { group1.Id, group2.Id }
        };

        // Act
        var count = await _service.ValidateDuplicatesAsync(request, CancellationToken.None);

        // Assert
        count.Should().Be(2);

        var validated = await _dbContext.DuplicateGroups
            .Where(g => g.Status == "validated")
            .ToListAsync();

        validated.Should().HaveCount(2);
        validated.Should().Contain(g => g.Id == group1.Id);
        validated.Should().Contain(g => g.Id == group2.Id);
        validated.All(g => g.ValidatedAt.HasValue).Should().BeTrue();

        var stillPending = await _dbContext.DuplicateGroups.FindAsync(group3.Id);
        stillPending!.Status.Should().Be("pending");
    }

    [Fact]
    public async Task ValidateDuplicatesAsync_SetsValidatedAtTimestamp()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        var beforeValidation = DateTime.UtcNow;
        var request = new ValidateDuplicatesRequest
        {
            GroupIds = new List<Guid> { group.Id }
        };

        // Act
        await _service.ValidateDuplicatesAsync(request, CancellationToken.None);

        // Assert
        var validated = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        validated!.ValidatedAt.Should().NotBeNull();
        validated.ValidatedAt.Should().BeOnOrAfter(beforeValidation);
        validated.ValidatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task ValidateDuplicatesAsync_SetsKeptFileId_WhenProvided()
    {
        // Arrange
        var keptFileId = Guid.NewGuid();
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        var request = new ValidateDuplicatesRequest
        {
            GroupIds = new List<Guid> { group.Id },
            KeptFileId = keptFileId
        };

        // Act
        await _service.ValidateDuplicatesAsync(request, CancellationToken.None);

        // Assert
        var validated = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        validated!.KeptFileId.Should().Be(keptFileId);
    }

    [Fact]
    public async Task ValidateDuplicatesAsync_DoesNotSetKeptFileId_WhenNotProvided()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        var request = new ValidateDuplicatesRequest
        {
            GroupIds = new List<Guid> { group.Id }
        };

        // Act
        await _service.ValidateDuplicatesAsync(request, CancellationToken.None);

        // Assert
        var validated = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        validated!.KeptFileId.Should().BeNull();
    }

    [Fact]
    public async Task ValidateDuplicatesAsync_ReturnsZero_WhenNoGroupsFound()
    {
        // Arrange
        var request = new ValidateDuplicatesRequest
        {
            GroupIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
        };

        // Act
        var count = await _service.ValidateDuplicatesAsync(request, CancellationToken.None);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task ValidateDuplicatesAsync_HandlesEmptyGroupIds()
    {
        // Arrange
        var request = new ValidateDuplicatesRequest
        {
            GroupIds = new List<Guid>()
        };

        // Act
        var count = await _service.ValidateDuplicatesAsync(request, CancellationToken.None);

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region ValidateBatchAsync Tests

    [Fact]
    public async Task ValidateBatchAsync_ValidatesRequestedCount()
    {
        // Arrange
        var groups = Enumerable.Range(1, 10).Select(i => new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = $"hash{i}",
            FileCount = 2,
            TotalSize = 1000 * i, // Different sizes for ordering
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _dbContext.DuplicateGroups.AddRangeAsync(groups);
        await _dbContext.SaveChangesAsync();

        var request = new ValidateBatchRequest
        {
            Count = 5
        };

        // Act
        var result = await _service.ValidateBatchAsync(request, CancellationToken.None);

        // Assert
        result.Validated.Should().Be(5);
        result.Remaining.Should().Be(5);

        var validated = await _dbContext.DuplicateGroups
            .Where(g => g.Status == "validated")
            .ToListAsync();

        validated.Should().HaveCount(5);
    }

    [Fact]
    public async Task ValidateBatchAsync_OrdersByTotalSizeDescending()
    {
        // Arrange
        var group1 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 1000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var group2 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 2,
            TotalSize = 5000, // Largest
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var group3 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash3",
            FileCount = 2,
            TotalSize = 3000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(group1, group2, group3);
        await _dbContext.SaveChangesAsync();

        var request = new ValidateBatchRequest
        {
            Count = 2
        };

        // Act
        await _service.ValidateBatchAsync(request, CancellationToken.None);

        // Assert
        var validated = await _dbContext.DuplicateGroups
            .Where(g => g.Status == "validated")
            .OrderByDescending(g => g.TotalSize)
            .ToListAsync();

        validated.Should().HaveCount(2);
        validated[0].Id.Should().Be(group2.Id); // Largest
        validated[1].Id.Should().Be(group3.Id); // Second largest
    }

    [Fact]
    public async Task ValidateBatchAsync_AppliesStatusFilter()
    {
        // Arrange
        var pendingGroups = Enumerable.Range(1, 5).Select(i => new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = $"pending{i}",
            FileCount = 2,
            TotalSize = 1000 * i,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        }).ToList();

        var autoSelectedGroups = Enumerable.Range(1, 3).Select(i => new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = $"auto{i}",
            FileCount = 2,
            TotalSize = 2000 * i,
            Status = "auto-selected",
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _dbContext.DuplicateGroups.AddRangeAsync(pendingGroups);
        await _dbContext.DuplicateGroups.AddRangeAsync(autoSelectedGroups);
        await _dbContext.SaveChangesAsync();

        var request = new ValidateBatchRequest
        {
            Count = 3,
            StatusFilter = "pending"
        };

        // Act
        var result = await _service.ValidateBatchAsync(request, CancellationToken.None);

        // Assert
        result.Validated.Should().Be(3);
        result.Remaining.Should().Be(2); // 5 pending - 3 validated = 2 remaining

        var validated = await _dbContext.DuplicateGroups
            .Where(g => g.Status == "validated")
            .ToListAsync();

        validated.Should().HaveCount(3);
        validated.All(g => pendingGroups.Select(p => p.Id).Contains(g.Id)).Should().BeTrue();

        var stillAutoSelected = await _dbContext.DuplicateGroups
            .Where(g => g.Status == "auto-selected")
            .ToListAsync();

        stillAutoSelected.Should().HaveCount(3);
    }

    [Fact]
    public async Task ValidateBatchAsync_HandlesCountGreaterThanAvailable()
    {
        // Arrange
        var groups = Enumerable.Range(1, 3).Select(i => new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = $"hash{i}",
            FileCount = 2,
            TotalSize = 1000 * i,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _dbContext.DuplicateGroups.AddRangeAsync(groups);
        await _dbContext.SaveChangesAsync();

        var request = new ValidateBatchRequest
        {
            Count = 10
        };

        // Act
        var result = await _service.ValidateBatchAsync(request, CancellationToken.None);

        // Assert
        result.Validated.Should().Be(3);
        result.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task ValidateBatchAsync_ReturnsZeroValidated_WhenNoGroupsAvailable()
    {
        // Arrange
        var request = new ValidateBatchRequest
        {
            Count = 5
        };

        // Act
        var result = await _service.ValidateBatchAsync(request, CancellationToken.None);

        // Assert
        result.Validated.Should().Be(0);
        result.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task ValidateBatchAsync_SetsValidatedAtForAllGroups()
    {
        // Arrange
        var groups = Enumerable.Range(1, 3).Select(i => new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = $"hash{i}",
            FileCount = 2,
            TotalSize = 1000 * i,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _dbContext.DuplicateGroups.AddRangeAsync(groups);
        await _dbContext.SaveChangesAsync();

        var beforeValidation = DateTime.UtcNow;
        var request = new ValidateBatchRequest
        {
            Count = 3
        };

        // Act
        await _service.ValidateBatchAsync(request, CancellationToken.None);

        // Assert
        var validated = await _dbContext.DuplicateGroups
            .Where(g => g.Status == "validated")
            .ToListAsync();

        validated.All(g => g.ValidatedAt.HasValue).Should().BeTrue();
        validated.All(g => g.ValidatedAt >= beforeValidation).Should().BeTrue();
    }

    #endregion

    #region UndoValidationAsync Tests

    [Fact]
    public async Task UndoValidationAsync_ResetsStatusToPending()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "validated",
            ValidatedAt = DateTime.UtcNow.AddHours(-1),
            KeptFileId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        var request = new UndoValidationRequest
        {
            GroupIds = new List<Guid> { group.Id }
        };

        // Act
        var count = await _service.UndoValidationAsync(request, CancellationToken.None);

        // Assert
        count.Should().Be(1);

        var updated = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        updated!.Status.Should().Be("pending");
        updated.ValidatedAt.Should().BeNull();
        updated.KeptFileId.Should().BeNull();
    }

    [Fact]
    public async Task UndoValidationAsync_HandlesMultipleGroups()
    {
        // Arrange
        var groups = Enumerable.Range(1, 5).Select(i => new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = $"hash{i}",
            FileCount = 2,
            TotalSize = 1000 * i,
            Status = "validated",
            ValidatedAt = DateTime.UtcNow.AddHours(-1),
            KeptFileId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _dbContext.DuplicateGroups.AddRangeAsync(groups);
        await _dbContext.SaveChangesAsync();

        var request = new UndoValidationRequest
        {
            GroupIds = groups.Take(3).Select(g => g.Id).ToList()
        };

        // Act
        var count = await _service.UndoValidationAsync(request, CancellationToken.None);

        // Assert
        count.Should().Be(3);

        var pending = await _dbContext.DuplicateGroups
            .Where(g => g.Status == "pending")
            .ToListAsync();

        pending.Should().HaveCount(3);
        pending.All(g => !g.ValidatedAt.HasValue).Should().BeTrue();
        pending.All(g => !g.KeptFileId.HasValue).Should().BeTrue();

        var stillValidated = await _dbContext.DuplicateGroups
            .Where(g => g.Status == "validated")
            .ToListAsync();

        stillValidated.Should().HaveCount(2);
    }

    [Fact]
    public async Task UndoValidationAsync_ReturnsZero_WhenNoGroupsFound()
    {
        // Arrange
        var request = new UndoValidationRequest
        {
            GroupIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
        };

        // Act
        var count = await _service.UndoValidationAsync(request, CancellationToken.None);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task UndoValidationAsync_HandlesEmptyGroupIds()
    {
        // Arrange
        var request = new UndoValidationRequest
        {
            GroupIds = new List<Guid>()
        };

        // Act
        var count = await _service.UndoValidationAsync(request, CancellationToken.None);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task UndoValidationAsync_ClearsAllValidationFields()
    {
        // Arrange
        var validatedAt = DateTime.UtcNow.AddHours(-2);
        var keptFileId = Guid.NewGuid();
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "validated",
            ValidatedAt = validatedAt,
            KeptFileId = keptFileId,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        var request = new UndoValidationRequest
        {
            GroupIds = new List<Guid> { group.Id }
        };

        // Act
        await _service.UndoValidationAsync(request, CancellationToken.None);

        // Assert
        var updated = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        updated!.Status.Should().Be("pending");
        updated.ValidatedAt.Should().BeNull();
        updated.KeptFileId.Should().BeNull();
    }

    #endregion

    #region Status Transition Tests

    [Fact]
    public async Task StatusTransition_PendingToValidated_WorksCorrectly()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            ValidatedAt = null,
            KeptFileId = null,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        // Act - Transition to validated
        var validateRequest = new ValidateDuplicatesRequest
        {
            GroupIds = new List<Guid> { group.Id },
            KeptFileId = Guid.NewGuid()
        };
        await _service.ValidateDuplicatesAsync(validateRequest, CancellationToken.None);

        // Assert
        var validated = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        validated!.Status.Should().Be("validated");
        validated.ValidatedAt.Should().NotBeNull();
        validated.KeptFileId.Should().NotBeNull();
    }

    [Fact]
    public async Task StatusTransition_ValidatedToPending_WorksCorrectly()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "validated",
            ValidatedAt = DateTime.UtcNow.AddHours(-1),
            KeptFileId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        // Act - Transition back to pending
        var undoRequest = new UndoValidationRequest
        {
            GroupIds = new List<Guid> { group.Id }
        };
        await _service.UndoValidationAsync(undoRequest, CancellationToken.None);

        // Assert
        var pending = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        pending!.Status.Should().Be("pending");
        pending.ValidatedAt.Should().BeNull();
        pending.KeptFileId.Should().BeNull();
    }

    [Fact]
    public async Task StatusTransition_CompleteRoundTrip_WorksCorrectly()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        var keptFileId = Guid.NewGuid();

        // Act & Assert - Pending to Validated
        var validateRequest = new ValidateDuplicatesRequest
        {
            GroupIds = new List<Guid> { group.Id },
            KeptFileId = keptFileId
        };
        await _service.ValidateDuplicatesAsync(validateRequest, CancellationToken.None);

        var afterValidation = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        afterValidation!.Status.Should().Be("validated");
        afterValidation.ValidatedAt.Should().NotBeNull();
        afterValidation.KeptFileId.Should().Be(keptFileId);

        // Act & Assert - Validated back to Pending
        var undoRequest = new UndoValidationRequest
        {
            GroupIds = new List<Guid> { group.Id }
        };
        await _service.UndoValidationAsync(undoRequest, CancellationToken.None);

        var afterUndo = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        afterUndo!.Status.Should().Be("pending");
        afterUndo.ValidatedAt.Should().BeNull();
        afterUndo.KeptFileId.Should().BeNull();

        // Act & Assert - Pending to Validated again
        var revalidateRequest = new ValidateDuplicatesRequest
        {
            GroupIds = new List<Guid> { group.Id }
        };
        await _service.ValidateDuplicatesAsync(revalidateRequest, CancellationToken.None);

        var afterRevalidation = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        afterRevalidation!.Status.Should().Be("validated");
        afterRevalidation.ValidatedAt.Should().NotBeNull();
        afterRevalidation.KeptFileId.Should().BeNull(); // Not provided in second validation
    }

    #endregion

    #region GetPatternForGroupAsync Tests

    [Fact]
    public async Task GetPatternForGroupAsync_ReturnsNull_WhenGroupNotFound()
    {
        // Act
        var result = await _service.GetPatternForGroupAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPatternForGroupAsync_ReturnsPatternInfo_WithCorrectDirectories()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 3,
            TotalSize = 3000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        var file1 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/2024/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group
        };
        var file2 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/backup/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.IndexedFiles.AddRangeAsync(file1, file2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPatternForGroupAsync(group.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Directories.Should().HaveCount(2);
        result.Directories.Should().Contain("/photos/2024");
        result.Directories.Should().Contain("/photos/backup");
    }

    [Fact]
    public async Task GetPatternForGroupAsync_FindsMatchingGroups_WithSamePattern()
    {
        // Arrange
        var group1 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var group2 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var group3 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash3",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        // Group1 files: /folderA, /folderB
        var file1a = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folderA/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group1
        };
        var file1b = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folderB/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group1
        };

        // Group2 files: /folderA, /folderB (same pattern)
        var file2a = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folderA/img2.jpg",
            FileName = "img2.jpg",
            FileHash = "hash2",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group2
        };
        var file2b = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folderB/img2.jpg",
            FileName = "img2.jpg",
            FileHash = "hash2",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group2
        };

        // Group3 files: /folderC, /folderD (different pattern)
        var file3a = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folderC/img3.jpg",
            FileName = "img3.jpg",
            FileHash = "hash3",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group3
        };
        var file3b = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folderD/img3.jpg",
            FileName = "img3.jpg",
            FileHash = "hash3",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group3
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(group1, group2, group3);
        await _dbContext.IndexedFiles.AddRangeAsync(file1a, file1b, file2a, file2b, file3a, file3b);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPatternForGroupAsync(group1.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.MatchingGroupCount.Should().Be(2); // group1 and group2
        result.GroupIds.Should().Contain(group1.Id);
        result.GroupIds.Should().Contain(group2.Id);
        result.GroupIds.Should().NotContain(group3.Id);
    }

    [Fact]
    public async Task GetPatternForGroupAsync_ExcludesHiddenFiles()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 3,
            TotalSize = 3000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        var visibleFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/photos/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            IsHidden = false,
            DuplicateGroup = group
        };
        var hiddenFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/hidden/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            IsHidden = true,
            DuplicateGroup = group
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.IndexedFiles.AddRangeAsync(visibleFile, hiddenFile);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPatternForGroupAsync(group.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Directories.Should().HaveCount(1);
        result.Directories.Should().Contain("/photos");
        result.Directories.Should().NotContain("/hidden");
    }

    #endregion

    #region ApplyPatternRuleAsync Tests

    [Fact]
    public async Task ApplyPatternRuleAsync_UpdatesMatchingGroups()
    {
        // Arrange
        var group1 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var group2 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        // Group1 files: /original, /backup
        var file1a = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/original/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            IsDuplicate = true,
            DuplicateGroup = group1
        };
        var file1b = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/backup/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            IsDuplicate = true,
            DuplicateGroup = group1
        };

        // Group2 files: /original, /backup (same pattern)
        var file2a = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/original/img2.jpg",
            FileName = "img2.jpg",
            FileHash = "hash2",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            IsDuplicate = true,
            DuplicateGroup = group2
        };
        var file2b = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/backup/img2.jpg",
            FileName = "img2.jpg",
            FileHash = "hash2",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            IsDuplicate = true,
            DuplicateGroup = group2
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(group1, group2);
        await _dbContext.IndexedFiles.AddRangeAsync(file1a, file1b, file2a, file2b);
        await _dbContext.SaveChangesAsync();

        var request = new ApplyPatternRuleRequest
        {
            Directories = new List<string> { "/backup", "/original" },
            PreferredDirectory = "/original"
        };

        // Act
        var result = await _service.ApplyPatternRuleAsync(request, CancellationToken.None);

        // Assert
        result.GroupsUpdated.Should().Be(2);
        result.FilesMarkedAsOriginal.Should().Be(2);

        // Verify files in /original are marked as not duplicate
        var updatedFile1a = await _dbContext.IndexedFiles.FindAsync(file1a.Id);
        var updatedFile2a = await _dbContext.IndexedFiles.FindAsync(file2a.Id);
        updatedFile1a!.IsDuplicate.Should().BeFalse();
        updatedFile2a!.IsDuplicate.Should().BeFalse();

        // Verify files in /backup are still marked as duplicate
        var updatedFile1b = await _dbContext.IndexedFiles.FindAsync(file1b.Id);
        var updatedFile2b = await _dbContext.IndexedFiles.FindAsync(file2b.Id);
        updatedFile1b!.IsDuplicate.Should().BeTrue();
        updatedFile2b!.IsDuplicate.Should().BeTrue();

        // Verify groups are auto-selected with kept file set
        var updatedGroup1 = await _dbContext.DuplicateGroups.FindAsync(group1.Id);
        var updatedGroup2 = await _dbContext.DuplicateGroups.FindAsync(group2.Id);
        updatedGroup1!.Status.Should().Be("auto-selected");
        updatedGroup1.KeptFileId.Should().Be(file1a.Id);
        updatedGroup2!.Status.Should().Be("auto-selected");
        updatedGroup2.KeptFileId.Should().Be(file2a.Id);
    }

    [Fact]
    public async Task ApplyPatternRuleAsync_SkipsGroupsWithNoFileInPreferredDirectory()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        // Group files: /folderA, /folderB (no file in preferred /folderC)
        var file1 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folderA/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group
        };
        var file2 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folderB/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.IndexedFiles.AddRangeAsync(file1, file2);
        await _dbContext.SaveChangesAsync();

        var request = new ApplyPatternRuleRequest
        {
            Directories = new List<string> { "/folderA", "/folderB" },
            PreferredDirectory = "/folderC" // No files in this directory
        };

        // Act
        var result = await _service.ApplyPatternRuleAsync(request, CancellationToken.None);

        // Assert
        result.GroupsUpdated.Should().Be(0);
        result.GroupsSkipped.Should().Be(1);
    }

    [Fact]
    public async Task ApplyPatternRuleAsync_ReturnsNextUnresolvedGroupWithDifferentPattern()
    {
        // Arrange
        var group1 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var group2 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5) // Created later
        };

        // Group1: /folderA, /folderB
        var file1a = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folderA/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group1
        };
        var file1b = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folderB/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group1
        };

        // Group2: /folderC, /folderD (different pattern)
        var file2a = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folderC/img2.jpg",
            FileName = "img2.jpg",
            FileHash = "hash2",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group2
        };
        var file2b = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folderD/img2.jpg",
            FileName = "img2.jpg",
            FileHash = "hash2",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            DuplicateGroup = group2
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(group1, group2);
        await _dbContext.IndexedFiles.AddRangeAsync(file1a, file1b, file2a, file2b);
        await _dbContext.SaveChangesAsync();

        var request = new ApplyPatternRuleRequest
        {
            Directories = new List<string> { "/folderA", "/folderB" },
            PreferredDirectory = "/folderA"
        };

        // Act
        var result = await _service.ApplyPatternRuleAsync(request, CancellationToken.None);

        // Assert
        result.NextUnresolvedGroupId.Should().Be(group2.Id);
    }

    #endregion

    #region GetNavigationAsync Tests

    [Fact]
    public async Task GetNavigationAsync_ReturnsCorrectNavigation_ForMiddleGroup()
    {
        // Arrange - Groups ordered by TotalSize descending
        var group1 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 1000, // Smallest
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var group2 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 2,
            TotalSize = 2000, // Middle
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var group3 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash3",
            FileCount = 2,
            TotalSize = 3000, // Largest
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(group1, group2, group3);
        await _dbContext.SaveChangesAsync();

        // Act - Get navigation for middle group
        var result = await _service.GetNavigationAsync(group2.Id, null, CancellationToken.None);

        // Assert
        result.PreviousGroupId.Should().Be(group3.Id); // Larger group comes before
        result.NextGroupId.Should().Be(group1.Id); // Smaller group comes after
        result.CurrentPosition.Should().Be(2);
        result.TotalGroups.Should().Be(3);
    }

    [Fact]
    public async Task GetNavigationAsync_ReturnsNullPrevious_ForFirstGroup()
    {
        // Arrange
        var group1 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 1000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var group2 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 2,
            TotalSize = 2000, // Largest - first in order
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(group1, group2);
        await _dbContext.SaveChangesAsync();

        // Act - Get navigation for first group (largest)
        var result = await _service.GetNavigationAsync(group2.Id, null, CancellationToken.None);

        // Assert
        result.PreviousGroupId.Should().BeNull();
        result.NextGroupId.Should().Be(group1.Id);
        result.CurrentPosition.Should().Be(1);
        result.TotalGroups.Should().Be(2);
    }

    [Fact]
    public async Task GetNavigationAsync_ReturnsNullNext_ForLastGroup()
    {
        // Arrange
        var group1 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 1000, // Smallest - last in order
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var group2 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(group1, group2);
        await _dbContext.SaveChangesAsync();

        // Act - Get navigation for last group (smallest)
        var result = await _service.GetNavigationAsync(group1.Id, null, CancellationToken.None);

        // Assert
        result.PreviousGroupId.Should().Be(group2.Id);
        result.NextGroupId.Should().BeNull();
        result.CurrentPosition.Should().Be(2);
        result.TotalGroups.Should().Be(2);
    }

    [Fact]
    public async Task GetNavigationAsync_AppliesStatusFilter()
    {
        // Arrange
        var pendingGroup1 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 1000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var pendingGroup2 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var validatedGroup = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash3",
            FileCount = 2,
            TotalSize = 3000, // Largest but validated
            Status = "validated",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(pendingGroup1, pendingGroup2, validatedGroup);
        await _dbContext.SaveChangesAsync();

        // Act - Get navigation with pending filter
        var result = await _service.GetNavigationAsync(pendingGroup2.Id, "pending", CancellationToken.None);

        // Assert
        result.PreviousGroupId.Should().BeNull(); // pendingGroup2 is first among pending
        result.NextGroupId.Should().Be(pendingGroup1.Id);
        result.CurrentPosition.Should().Be(1);
        result.TotalGroups.Should().Be(2); // Only pending groups
    }

    [Fact]
    public async Task GetNavigationAsync_ReturnsZeroPosition_WhenGroupNotFound()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 1000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        // Act - Get navigation for non-existent group
        var result = await _service.GetNavigationAsync(Guid.NewGuid(), null, CancellationToken.None);

        // Assert
        result.PreviousGroupId.Should().BeNull();
        result.NextGroupId.Should().BeNull();
        result.CurrentPosition.Should().Be(0);
        result.TotalGroups.Should().Be(1);
    }

    #endregion

    #region Session Tests

    [Fact]
    public async Task StartOrResumeSessionAsync_CreatesNewSession_WhenNoExistingSession()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.StartOrResumeSessionAsync(true, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("active");
        result.TotalGroups.Should().Be(1);
        result.GroupsProposed.Should().Be(0);
        result.GroupsValidated.Should().Be(0);
        result.GroupsSkipped.Should().Be(0);
    }

    [Fact]
    public async Task StartOrResumeSessionAsync_ResumesExistingSession_WhenResumeIsTrue()
    {
        // Arrange
        var existingSession = new SelectionSession
        {
            Id = Guid.NewGuid(),
            Status = "paused",
            TotalGroups = 10,
            GroupsProposed = 3,
            GroupsValidated = 2,
            GroupsSkipped = 1,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-30)
        };

        await _dbContext.SelectionSessions.AddAsync(existingSession);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.StartOrResumeSessionAsync(true, CancellationToken.None);

        // Assert
        result.Id.Should().Be(existingSession.Id);
        result.Status.Should().Be("active");
        result.GroupsProposed.Should().Be(3);
        result.GroupsValidated.Should().Be(2);
        result.GroupsSkipped.Should().Be(1);
        result.ResumedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task StartOrResumeSessionAsync_CreatesNewSession_WhenResumeIsFalse()
    {
        // Arrange
        var existingSession = new SelectionSession
        {
            Id = Guid.NewGuid(),
            Status = "active",
            TotalGroups = 10,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        await _dbContext.SelectionSessions.AddAsync(existingSession);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.StartOrResumeSessionAsync(false, CancellationToken.None);

        // Assert
        result.Id.Should().NotBe(existingSession.Id);
        result.Status.Should().Be("active");
    }

    [Fact]
    public async Task GetCurrentSessionAsync_ReturnsNull_WhenNoActiveSession()
    {
        // Act
        var result = await _service.GetCurrentSessionAsync(CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentSessionAsync_ReturnsSession_WhenActiveSessionExists()
    {
        // Arrange
        var session = new SelectionSession
        {
            Id = Guid.NewGuid(),
            Status = "active",
            TotalGroups = 5,
            GroupsProposed = 2,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.SelectionSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentSessionAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(session.Id);
        result.Status.Should().Be("active");
    }

    [Fact]
    public async Task PauseSessionAsync_SetsStatusToPaused()
    {
        // Arrange
        var session = new SelectionSession
        {
            Id = Guid.NewGuid(),
            Status = "active",
            TotalGroups = 5,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.SelectionSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.PauseSessionAsync(session.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be("paused");
        result.LastActivityAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProposeOriginalAsync_SetsFileAsOriginal_AndUpdatesGroupStatus()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        var file1 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folder/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            IsDuplicate = true,
            DuplicateGroup = group
        };
        var file2 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folder/img2.jpg",
            FileName = "img2.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            IsDuplicate = true,
            DuplicateGroup = group
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.IndexedFiles.AddRangeAsync(file1, file2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ProposeOriginalAsync(group.Id, file1.Id, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("File proposed as original");

        var updatedFile1 = await _dbContext.IndexedFiles.FindAsync(file1.Id);
        var updatedFile2 = await _dbContext.IndexedFiles.FindAsync(file2.Id);
        updatedFile1!.IsDuplicate.Should().BeFalse();
        updatedFile2!.IsDuplicate.Should().BeTrue();

        var updatedGroup = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        updatedGroup!.Status.Should().Be("proposed");
        updatedGroup.KeptFileId.Should().Be(file1.Id);
    }

    [Fact]
    public async Task ValidateGroupAsync_SetsStatusToValidated()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "proposed",
            KeptFileId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ValidateGroupAsync(group.Id, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Group validated");

        var updatedGroup = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        updatedGroup!.Status.Should().Be("validated");
        updatedGroup.ValidatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateGroupAsync_Fails_WhenNoProposedOriginal()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            KeptFileId = null,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ValidateGroupAsync(group.Id, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Group has no proposed original");
    }

    [Fact]
    public async Task SkipGroupAsync_UpdatesLastReviewedAt_AndReturnsNextGroup()
    {
        // Arrange
        var group1 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var group2 = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash2",
            FileCount = 2,
            TotalSize = 1000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.DuplicateGroups.AddRangeAsync(group1, group2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.SkipGroupAsync(group1.Id, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.NextGroupId.Should().Be(group2.Id);

        var updatedGroup = await _dbContext.DuplicateGroups.FindAsync(group1.Id);
        updatedGroup!.LastReviewedAt.Should().NotBeNull();
        updatedGroup.Status.Should().Be("pending"); // Status unchanged
    }

    [Fact]
    public async Task UndoLastActionAsync_ResetsToPending()
    {
        // Arrange
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "proposed",
            KeptFileId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        var file1 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folder/img1.jpg",
            FileName = "img1.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            IsDuplicate = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            DuplicateGroup = group
        };
        var file2 = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = "/folder/img2.jpg",
            FileName = "img2.jpg",
            FileHash = "hash1",
            FileSize = 1000,
            IndexedAt = DateTime.UtcNow,
            IsDuplicate = true,
            CreatedAt = DateTime.UtcNow,
            DuplicateGroup = group
        };

        await _dbContext.DuplicateGroups.AddAsync(group);
        await _dbContext.IndexedFiles.AddRangeAsync(file1, file2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.UndoLastActionAsync(group.Id, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Reverted from proposed to pending");

        var updatedGroup = await _dbContext.DuplicateGroups.FindAsync(group.Id);
        updatedGroup!.Status.Should().Be("pending");
        updatedGroup.KeptFileId.Should().BeNull();
        updatedGroup.ValidatedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetSessionProgressAsync_ReturnsCorrectProgress()
    {
        // Arrange
        var session = new SelectionSession
        {
            Id = Guid.NewGuid(),
            Status = "active",
            TotalGroups = 10,
            GroupsProposed = 3,
            GroupsValidated = 2,
            GroupsSkipped = 1,
            CreatedAt = DateTime.UtcNow
        };

        var pendingGroup = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = "hash1",
            FileCount = 2,
            TotalSize = 2000,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.SelectionSessions.AddAsync(session);
        await _dbContext.DuplicateGroups.AddAsync(pendingGroup);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetSessionProgressAsync(session.Id, CancellationToken.None);

        // Assert
        result.Proposed.Should().Be(3);
        result.Validated.Should().Be(2);
        result.Skipped.Should().Be(1);
        result.Remaining.Should().Be(1);
        result.ProgressPercent.Should().Be(60); // 6 out of 10
    }

    #endregion
}
