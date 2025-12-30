# ADR-014: DuplicateGroup Status Workflow

**Status**: Accepted
**Date**: 2025-12-30
**Author**: Claude Code
**Supersedes**: Partial content in backlog item `12-duplicate-management/002-batch-validation.md`

## Context

The Photos Index application manages duplicate file groups through a lifecycle from detection to cleanup. The current implementation uses inconsistent magic strings for status tracking, leading to:

1. **Confusion between statuses**: `proposed`, `auto-selected`, and `validated` have overlapping meanings
2. **Missing intermediate states**: No visibility when cleaning is in progress
3. **No error tracking**: Failed cleaning operations are not clearly surfaced
4. **Mixed human/algorithm decisions**: Cannot distinguish between user choices and algorithm suggestions

### Use Cases Requiring Clear Status Tracking

1. **Ingestion**: New duplicate groups discovered during indexing
2. **Algorithm Selection**: Auto-selection based on configurable weights/heuristics
3. **Manual Selection**: User explicitly chooses which file to keep (individual or via pattern)
4. **Cleaning Process**: Archiving to MinIO and deleting from Synology
5. **Error Recovery**: Handling failures during the cleaning process

### Key Requirement

**Algorithm selections must be re-runnable** when parameters change, while **human decisions must be protected** from being overwritten.

## Decision

### 1. Define Six Explicit Statuses

Implement a `DuplicateGroupStatus` enum with exactly six values:

```csharp
public enum DuplicateGroupStatus
{
    /// <summary>
    /// Initial state. No original file selected yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Algorithm has suggested an original file. Can be overwritten by re-running algorithm.
    /// </summary>
    AutoSelected = 1,

    /// <summary>
    /// Human has explicitly chosen the original (manual selection or pattern apply).
    /// Protected from algorithm overwrites.
    /// </summary>
    Validated = 2,

    /// <summary>
    /// Cleaning job is actively processing this group's files.
    /// </summary>
    Cleaning = 3,

    /// <summary>
    /// Cleaning failed for one or more files. Requires user intervention.
    /// </summary>
    CleaningFailed = 4,

    /// <summary>
    /// All non-original files successfully archived and deleted.
    /// </summary>
    Cleaned = 5
}
```

### 2. Status Transition Rules

```
                            ┌─────────────────────────────────────────────────┐
                            │                                                 │
                            ▼                                                 │
┌─────────┐  algo    ┌──────────────┐  re-run algo   ┌──────────────┐        │
│ Pending │─────────▶│ AutoSelected │◀──────────────▶│ AutoSelected │        │
└─────────┘          └──────────────┘                └──────────────┘        │
     │                      │                                                 │
     │                      │ manual validate                                 │
     │                      ▼                                                 │
     │  manual/     ┌─────────────┐      start clean     ┌──────────┐        │
     └─────────────▶│  Validated  │─────────────────────▶│ Cleaning │        │
                    └─────────────┘                      └──────────┘        │
                            ▲                                  │             │
                            │                          ┌───────┴───────┐    │
                            │                          │               │    │
                            │ retry                    ▼               ▼    │
                    ┌────────────────┐           ┌─────────┐    ┌─────────┐ │
                    │ CleaningFailed │◀──────────│  fail   │    │ success │ │
                    └────────────────┘           └─────────┘    └─────────┘ │
                            │                                        │      │
                            │ reset                                  ▼      │
                            │                                 ┌─────────┐   │
                            └─────────────────────────────────│ Cleaned │───┘
                                          undo (admin)        └─────────┘
```

### 3. Valid Transitions Matrix

| From | To | Trigger | Description |
|------|-----|---------|-------------|
| `Pending` | `AutoSelected` | Algorithm execution | Auto-select based on weights/heuristics |
| `Pending` | `Validated` | Manual selection / Pattern apply | User explicitly chooses original |
| `AutoSelected` | `AutoSelected` | Re-run algorithm | Algorithm overwrites previous suggestion |
| `AutoSelected` | `Validated` | Manual validation | User confirms algorithm suggestion |
| `AutoSelected` | `Pending` | Undo | User resets selection |
| `Validated` | `Cleaning` | Start clean job | Files queued for deletion |
| `Validated` | `Pending` | Undo (UI/Admin) | User resets selection |
| `Cleaning` | `Cleaned` | All files processed OK | Cleaning completed successfully |
| `Cleaning` | `CleaningFailed` | One or more files failed | Partial failure |
| `CleaningFailed` | `Validated` | Retry | User wants to retry cleaning |
| `CleaningFailed` | `Pending` | Reset | User wants to start over |
| `Cleaned` | `Pending` | Admin reset | Database-level reset only |

### 4. Invalid Transitions (Must Be Prevented)

| From | To | Reason |
|------|-----|--------|
| `Validated` | `AutoSelected` | Human decisions cannot be overwritten by algorithm |
| `Cleaning` | `AutoSelected` | Cannot modify while cleaning in progress |
| `Cleaning` | `Pending` | Cannot reset while cleaning in progress |
| `Cleaned` | `AutoSelected` | Cleaned files are deleted, cannot re-select |
| `Cleaned` | `Validated` | Cannot validate already cleaned group |
| Any | `Cleaning` | Only from `Validated` via clean job creation |

### 5. Algorithm Re-run Behavior

When re-running the selection algorithm:

```csharp
// Only process groups that can be overwritten
var eligibleGroups = await _dbContext.DuplicateGroups
    .Where(g => g.Status == DuplicateGroupStatus.Pending
             || g.Status == DuplicateGroupStatus.AutoSelected)
    .ToListAsync(ct);

// Validated, Cleaning, CleaningFailed, Cleaned are never touched
```

### 6. Database Changes

```sql
-- Add enum-backed status column (PostgreSQL)
ALTER TABLE "DuplicateGroups"
    ALTER COLUMN "Status" TYPE INTEGER USING
        CASE "Status"
            WHEN 'pending' THEN 0
            WHEN 'auto-selected' THEN 1
            WHEN 'proposed' THEN 1  -- Map legacy 'proposed' to AutoSelected
            WHEN 'validated' THEN 2
            WHEN 'cleaning' THEN 3
            WHEN 'cleaning-failed' THEN 4
            WHEN 'cleaned' THEN 5
            ELSE 0
        END;

-- Add index for common queries
CREATE INDEX idx_duplicate_groups_status ON "DuplicateGroups"("Status");
```

### 7. Cleaning Job Integration

When a clean job is created:

1. All target groups transition to `Cleaning` immediately
2. CleanerService processes files one by one
3. On each file completion, check if all files in group are done:
   - All succeeded → `Cleaned`
   - Any failed → `CleaningFailed`

```csharp
// In CleanerController.ConfirmDelete
private async Task UpdateGroupStatusAfterFileProcessed(Guid fileId, bool success)
{
    var file = await _dbContext.IndexedFiles
        .Include(f => f.DuplicateGroup)
        .FirstOrDefaultAsync(f => f.Id == fileId);

    if (file?.DuplicateGroup == null) return;

    var group = file.DuplicateGroup;
    var allFilesInJob = await GetJobFilesForGroup(group.Id);

    if (allFilesInJob.All(f => f.Status == CleanerFileStatus.Deleted || f.Status == CleanerFileStatus.Skipped))
    {
        group.Status = DuplicateGroupStatus.Cleaned;
    }
    else if (allFilesInJob.Any(f => f.Status == CleanerFileStatus.Failed))
    {
        group.Status = DuplicateGroupStatus.CleaningFailed;
    }
    // else: still Cleaning, some files not yet processed
}
```

### 8. Frontend Changes

#### Status Filter Options
```typescript
export enum DuplicateGroupStatus {
  Pending = 0,
  AutoSelected = 1,
  Validated = 2,
  Cleaning = 3,
  CleaningFailed = 4,
  Cleaned = 5
}

export const STATUS_OPTIONS = [
  { value: '', label: 'All' },
  { value: DuplicateGroupStatus.Pending, label: 'Pending' },
  { value: DuplicateGroupStatus.AutoSelected, label: 'Auto-selected' },
  { value: DuplicateGroupStatus.Validated, label: 'Validated' },
  { value: DuplicateGroupStatus.Cleaning, label: 'Cleaning...' },
  { value: DuplicateGroupStatus.CleaningFailed, label: 'Failed' },
  { value: DuplicateGroupStatus.Cleaned, label: 'Cleaned' },
];
```

#### Visual Indicators

| Status | Color | Icon | Description |
|--------|-------|------|-------------|
| `Pending` | Gray | `help_outline` | Needs attention |
| `AutoSelected` | Blue | `auto_fix_high` | Algorithm suggestion |
| `Validated` | Purple | `check_circle` | Human decision |
| `Cleaning` | Orange | `sync` (spinning) | In progress |
| `CleaningFailed` | Red | `error` | Action required |
| `Cleaned` | Green | `delete_sweep` | Completed |

## Consequences

### Positive

- **Clear semantic meaning**: Each status has one unambiguous meaning
- **Type safety**: Enum prevents invalid status strings
- **Algorithm protection**: Human decisions are protected from auto-overwrite
- **Visibility**: Cleaning progress and failures are clearly visible
- **Testable**: Transition rules can be unit tested
- **Auditable**: Status changes can be logged with timestamps

### Negative

- **Migration required**: Existing data must be migrated from strings to enum
- **Breaking change**: API responses change from string to integer
- **UI update**: Frontend must handle new statuses

### Neutral

- **Six statuses vs three**: More states to manage, but each serves a distinct purpose
- **Database index**: Additional index for status queries

## Implementation Plan

See backlog item: `docs/backlog/12-duplicate-management/006-status-workflow.md`

## References

- ADR-013: Cleaner Service Architecture
- Backlog: `docs/backlog/12-duplicate-management/002-batch-validation.md` (superseded)
- Backlog: `docs/backlog/04-cleaner-service/002-smart-duplicate-selection.md`
