# 006: DuplicateGroup Status Workflow Implementation

**Status**: 🔲 Not Started
**Priority**: P0 (Critical)
**Branch**: `feature/duplicate-group-status-enum`
**Estimated Complexity**: High
**Target Release**: v0.10.0
**ADR**: [ADR-014](../../adrs/014-duplicate-group-status-workflow.md)
**Supersedes**: [002-batch-validation.md](./002-batch-validation.md) (status-related sections)

## Objective

Implement a robust, type-safe status workflow for DuplicateGroup entities using an enum instead of magic strings. This ensures clear semantics, prevents invalid transitions, and provides visibility into the cleaning process.

## Background

The current implementation uses inconsistent string-based statuses (`pending`, `proposed`, `auto-selected`, `validated`, `cleaned`) leading to confusion and bugs. This task consolidates into 6 clear statuses with enforced transition rules.

## Status Definitions

| Status | Value | Description | Can Re-run Algo? |
|--------|-------|-------------|------------------|
| `Pending` | 0 | No original selected | Yes |
| `AutoSelected` | 1 | Algorithm suggested original | Yes |
| `Validated` | 2 | Human chose original | No |
| `Cleaning` | 3 | Clean job in progress | No |
| `CleaningFailed` | 4 | Clean job failed | No |
| `Cleaned` | 5 | Successfully cleaned | No |

## Acceptance Criteria

### Backend

- [ ] Create `DuplicateGroupStatus` enum in `src/Database/Enums/`
- [ ] Update `DuplicateGroup` entity to use enum instead of string
- [ ] Create EF Core migration for data type change with legacy data mapping
- [ ] Implement `DuplicateGroupStatusTransitionValidator` service
- [ ] Update `DuplicateService` to use enum and validate transitions
- [ ] Update `OriginalSelectionService` to set `AutoSelected` status
- [ ] Update `ApplyPatternRuleAsync` to set `Validated` status directly
- [ ] Update `AutoSelectAllAsync` to only process `Pending` and `AutoSelected` groups
- [ ] Update `CleanerController.ConfirmDelete` to transition group status
- [ ] Update all API DTOs to use integer status values
- [ ] Add `CleaningStartedAt` and `CleaningCompletedAt` timestamps

### Frontend

- [ ] Create `DuplicateGroupStatus` TypeScript enum matching backend
- [ ] Update status filter dropdown with all 6 options
- [ ] Implement status-specific icons and colors
- [ ] Add spinner animation for `Cleaning` status
- [ ] Show error indicator for `CleaningFailed` status
- [ ] Update `getStatusClass()` and `getStatusLabel()` methods
- [ ] Disable action buttons based on status (e.g., no clean while cleaning)

### Testing

- [ ] Unit tests: All valid status transitions
- [ ] Unit tests: All invalid status transitions (expect exceptions)
- [ ] Unit tests: Algorithm re-run only affects `Pending`/`AutoSelected`
- [ ] Integration tests: Full workflow `Pending` → `Validated` → `Cleaning` → `Cleaned`
- [ ] Integration tests: Failure scenario `Cleaning` → `CleaningFailed` → retry
- [ ] E2E tests: UI status filter functionality
- [ ] E2E tests: Visual indicators for each status

## Technical Design

### 1. Enum Definition

```csharp
// src/Database/Enums/DuplicateGroupStatus.cs
namespace Database.Enums;

public enum DuplicateGroupStatus
{
    Pending = 0,
    AutoSelected = 1,
    Validated = 2,
    Cleaning = 3,
    CleaningFailed = 4,
    Cleaned = 5
}
```

### 2. Entity Update

```csharp
// src/Database/Entities/DuplicateGroup.cs
public class DuplicateGroup
{
    // ... existing fields

    public DuplicateGroupStatus Status { get; set; } = DuplicateGroupStatus.Pending;
    public DateTime? ValidatedAt { get; set; }
    public DateTime? CleaningStartedAt { get; set; }
    public DateTime? CleaningCompletedAt { get; set; }
    public Guid? KeptFileId { get; set; }
}
```

### 3. Transition Validator

```csharp
// src/Api/Services/DuplicateGroupStatusTransitionValidator.cs
public class DuplicateGroupStatusTransitionValidator
{
    private static readonly HashSet<(DuplicateGroupStatus From, DuplicateGroupStatus To)> ValidTransitions = new()
    {
        (DuplicateGroupStatus.Pending, DuplicateGroupStatus.AutoSelected),
        (DuplicateGroupStatus.Pending, DuplicateGroupStatus.Validated),
        (DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.AutoSelected),
        (DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Validated),
        (DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Pending),
        (DuplicateGroupStatus.Validated, DuplicateGroupStatus.Cleaning),
        (DuplicateGroupStatus.Validated, DuplicateGroupStatus.Pending),
        (DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.Cleaned),
        (DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.CleaningFailed),
        (DuplicateGroupStatus.CleaningFailed, DuplicateGroupStatus.Validated),
        (DuplicateGroupStatus.CleaningFailed, DuplicateGroupStatus.Pending),
        (DuplicateGroupStatus.Cleaned, DuplicateGroupStatus.Pending), // Admin only
    };

    public bool CanTransition(DuplicateGroupStatus from, DuplicateGroupStatus to)
        => ValidTransitions.Contains((from, to));

    public void ValidateTransition(DuplicateGroupStatus from, DuplicateGroupStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException(
                $"Invalid status transition from {from} to {to}");
    }
}
```

### 4. Migration

```csharp
// src/Database/Migrations/YYYYMMDDHHMMSS_ConvertStatusToEnum.cs
public partial class ConvertStatusToEnum : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add new integer column
        migrationBuilder.AddColumn<int>(
            name: "StatusNew",
            table: "DuplicateGroups",
            nullable: false,
            defaultValue: 0);

        // Migrate data
        migrationBuilder.Sql(@"
            UPDATE ""DuplicateGroups""
            SET ""StatusNew"" = CASE ""Status""
                WHEN 'pending' THEN 0
                WHEN 'auto-selected' THEN 1
                WHEN 'proposed' THEN 1
                WHEN 'validated' THEN 2
                WHEN 'cleaning' THEN 3
                WHEN 'cleaning-failed' THEN 4
                WHEN 'cleaned' THEN 5
                ELSE 0
            END
        ");

        // Drop old column and rename new
        migrationBuilder.DropColumn(name: "Status", table: "DuplicateGroups");
        migrationBuilder.RenameColumn(
            name: "StatusNew",
            table: "DuplicateGroups",
            newName: "Status");

        // Add new timestamp columns
        migrationBuilder.AddColumn<DateTime>(
            name: "CleaningStartedAt",
            table: "DuplicateGroups",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CleaningCompletedAt",
            table: "DuplicateGroups",
            nullable: true);

        // Create index
        migrationBuilder.CreateIndex(
            name: "IX_DuplicateGroups_Status",
            table: "DuplicateGroups",
            column: "Status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverse migration...
    }
}
```

### 5. Service Updates

```csharp
// In DuplicateService.cs

public async Task<int> AutoSelectAllAsync(AutoSelectRequest request, CancellationToken ct)
{
    // Only process Pending and AutoSelected groups
    var groups = await _dbContext.DuplicateGroups
        .Include(g => g.Files)
        .Where(g => g.Status == DuplicateGroupStatus.Pending
                 || g.Status == DuplicateGroupStatus.AutoSelected)
        .ToListAsync(ct);

    foreach (var group in groups)
    {
        // ... selection logic
        group.Status = DuplicateGroupStatus.AutoSelected;
        group.KeptFileId = selectedFile.Id;
    }

    await _dbContext.SaveChangesAsync(ct);
    return groups.Count;
}

public async Task<ApplyPatternRuleResultDto> ApplyPatternRuleAsync(
    ApplyPatternRuleRequest request, CancellationToken ct)
{
    // ... pattern matching logic

    foreach (var group in matchingGroups)
    {
        group.Status = DuplicateGroupStatus.Validated;
        group.ValidatedAt = DateTime.UtcNow;
        group.KeptFileId = selectedFile.Id;
    }

    // ...
}
```

### 6. Cleaner Integration

```csharp
// In CleanerController.cs or CleanerJobService.cs

public async Task<Guid> CreateCleanJobAsync(List<Guid> groupIds, CancellationToken ct)
{
    var groups = await _dbContext.DuplicateGroups
        .Where(g => groupIds.Contains(g.Id))
        .ToListAsync(ct);

    // Validate all groups are in Validated status
    foreach (var group in groups)
    {
        if (group.Status != DuplicateGroupStatus.Validated)
            throw new InvalidOperationException(
                $"Group {group.Id} is not in Validated status");

        group.Status = DuplicateGroupStatus.Cleaning;
        group.CleaningStartedAt = DateTime.UtcNow;
    }

    // Create job and files...
    await _dbContext.SaveChangesAsync(ct);
    return job.Id;
}

private async Task UpdateGroupStatusAfterFileProcessed(
    Guid groupId, CancellationToken ct)
{
    var group = await _dbContext.DuplicateGroups
        .FirstOrDefaultAsync(g => g.Id == groupId, ct);

    if (group == null || group.Status != DuplicateGroupStatus.Cleaning)
        return;

    var jobFiles = await _dbContext.CleanerJobFiles
        .Where(f => f.File.DuplicateGroupId == groupId)
        .ToListAsync(ct);

    var allProcessed = jobFiles.All(f =>
        f.Status == CleanerFileStatus.Deleted ||
        f.Status == CleanerFileStatus.Skipped ||
        f.Status == CleanerFileStatus.Failed);

    if (!allProcessed) return;

    var anyFailed = jobFiles.Any(f => f.Status == CleanerFileStatus.Failed);

    group.Status = anyFailed
        ? DuplicateGroupStatus.CleaningFailed
        : DuplicateGroupStatus.Cleaned;
    group.CleaningCompletedAt = DateTime.UtcNow;

    await _dbContext.SaveChangesAsync(ct);
}
```

### 7. Frontend Enum

```typescript
// src/Web/src/app/models/duplicate-group-status.enum.ts
export enum DuplicateGroupStatus {
  Pending = 0,
  AutoSelected = 1,
  Validated = 2,
  Cleaning = 3,
  CleaningFailed = 4,
  Cleaned = 5
}

export const STATUS_CONFIG: Record<DuplicateGroupStatus, {
  label: string;
  icon: string;
  color: string;
  cssClass: string;
}> = {
  [DuplicateGroupStatus.Pending]: {
    label: 'Pending',
    icon: 'help_outline',
    color: '#9e9e9e',
    cssClass: 'status-pending'
  },
  [DuplicateGroupStatus.AutoSelected]: {
    label: 'Auto-selected',
    icon: 'auto_fix_high',
    color: '#2196f3',
    cssClass: 'status-auto-selected'
  },
  [DuplicateGroupStatus.Validated]: {
    label: 'Validated',
    icon: 'check_circle',
    color: '#9c27b0',
    cssClass: 'status-validated'
  },
  [DuplicateGroupStatus.Cleaning]: {
    label: 'Cleaning...',
    icon: 'sync',
    color: '#ff9800',
    cssClass: 'status-cleaning'
  },
  [DuplicateGroupStatus.CleaningFailed]: {
    label: 'Failed',
    icon: 'error',
    color: '#f44336',
    cssClass: 'status-failed'
  },
  [DuplicateGroupStatus.Cleaned]: {
    label: 'Cleaned',
    icon: 'delete_sweep',
    color: '#4caf50',
    cssClass: 'status-cleaned'
  }
};
```

## Files to Create/Modify

### New Files

| File | Description |
|------|-------------|
| `src/Database/Enums/DuplicateGroupStatus.cs` | Status enum definition |
| `src/Api/Services/DuplicateGroupStatusTransitionValidator.cs` | Transition validation |
| `src/Database/Migrations/*_ConvertStatusToEnum.cs` | Data migration |
| `src/Web/src/app/models/duplicate-group-status.enum.ts` | Frontend enum |
| `tests/Api.Tests/Services/StatusTransitionValidatorTests.cs` | Transition tests |
| `tests/Integration.Tests/DuplicateGroupStatusWorkflowTests.cs` | Workflow tests |

### Modified Files

| File | Changes |
|------|---------|
| `src/Database/Entities/DuplicateGroup.cs` | Change Status to enum, add timestamps |
| `src/Database/PhotosDbContext.cs` | Configure enum conversion |
| `src/Api/Services/DuplicateService.cs` | Use enum, validate transitions |
| `src/Api/Services/OriginalSelectionService.cs` | Return enum status |
| `src/Api/Controllers/DuplicateGroupsController.cs` | Use enum in DTOs |
| `src/Api/Controllers/CleanerController.cs` | Update group status on clean |
| `src/Shared/Dtos/DuplicateGroupDto.cs` | Change status type |
| `src/Web/src/app/models/duplicate-group.dto.ts` | Change status type |
| `src/Web/src/app/features/duplicates/components/*` | Update status handling |

## Test Plan

### Unit Tests

```csharp
[Theory]
[InlineData(DuplicateGroupStatus.Pending, DuplicateGroupStatus.AutoSelected, true)]
[InlineData(DuplicateGroupStatus.Pending, DuplicateGroupStatus.Validated, true)]
[InlineData(DuplicateGroupStatus.AutoSelected, DuplicateGroupStatus.Validated, true)]
[InlineData(DuplicateGroupStatus.Validated, DuplicateGroupStatus.AutoSelected, false)]
[InlineData(DuplicateGroupStatus.Cleaning, DuplicateGroupStatus.Pending, false)]
[InlineData(DuplicateGroupStatus.Cleaned, DuplicateGroupStatus.Validated, false)]
public void CanTransition_ReturnsExpectedResult(
    DuplicateGroupStatus from,
    DuplicateGroupStatus to,
    bool expected)
{
    var validator = new DuplicateGroupStatusTransitionValidator();
    Assert.Equal(expected, validator.CanTransition(from, to));
}
```

### Integration Tests

```csharp
[Fact]
public async Task FullWorkflow_PendingToCleanedSuccessfully()
{
    // Arrange: Create pending group
    // Act: Auto-select → Validate → Clean
    // Assert: Status is Cleaned
}

[Fact]
public async Task CleaningFailure_TransitionsToCleaningFailed()
{
    // Arrange: Create validated group, mock file delete failure
    // Act: Start clean job
    // Assert: Status is CleaningFailed
}

[Fact]
public async Task AlgorithmRerun_DoesNotAffectValidatedGroups()
{
    // Arrange: Create one Pending, one Validated group
    // Act: Run auto-select all
    // Assert: Only Pending group changed
}
```

### E2E Tests

```typescript
test('status filter shows correct groups', async ({ page }) => {
  await page.goto('/duplicates');

  // Filter by Validated
  await page.getByLabel('Status').selectOption('2');
  await expect(page.locator('.status-validated')).toBeVisible();
  await expect(page.locator('.status-pending')).not.toBeVisible();
});

test('cleaning status shows spinner', async ({ page }) => {
  // ... trigger clean job
  await expect(page.locator('.status-cleaning .mat-icon')).toHaveClass(/rotating/);
});
```

## Rollout Plan

1. **Phase 1**: Create enum, migration, validator (no behavior change)
2. **Phase 2**: Update backend services to use enum
3. **Phase 3**: Update frontend to use enum
4. **Phase 4**: Add `Cleaning` and `CleaningFailed` integration
5. **Phase 5**: Comprehensive testing and bug fixes

## Dependencies

- Existing CleanerService and CleanerJob infrastructure (ADR-013)
- EF Core migrations working correctly
- TestContainers for integration tests

## Risk Mitigation

- **Data migration failure**: Test migration on copy of production data first
- **API breaking change**: Version API or coordinate frontend deployment
- **Status desync**: Add database constraints and transaction handling
