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
}
