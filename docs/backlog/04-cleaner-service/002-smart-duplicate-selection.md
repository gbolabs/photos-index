# Smart Duplicate Selection

## Overview

Implement intelligent auto-selection of which duplicate file to keep and which to delete, based on configurable heuristics. The system should analyze file paths, names, metadata, and quality to make optimal decisions.

## Goals

1. Automatically select the "best" file to keep from each duplicate group
2. Rename kept files to preserve the most informative filename
3. Provide confidence scoring and manual review for edge cases
4. Support user-configurable rules and path priorities

## Selection Heuristics

### 1. Path Priority

Files in certain directories are preferred over others. Default priority (highest to lowest):

| Priority | Path Pattern | Rationale |
|----------|--------------|-----------|
| 1 | `/photos`, `/albums`, `/library` | Primary photo storage |
| 2 | `/camera`, `/imports` | Direct camera imports |
| 3 | `/backup`, `/archive` | Backup locations |
| 4 | `/downloads`, `/transfer` | Temporary transfer locations |
| 5 | `/temp`, `/tmp`, `/cache` | Temporary directories |

**Configuration:**
```json
{
  "pathPriorities": [
    { "pattern": "/photos/**", "priority": 100 },
    { "pattern": "/albums/**", "priority": 100 },
    { "pattern": "/temp/**", "priority": 10 },
    { "pattern": "/transfer/**", "priority": 20 }
  ]
}
```

### 2. Filename Intelligence

#### Prefer Descriptive Names

| Preferred | Over | Reason |
|-----------|------|--------|
| `Christmas Dinner 2023.jpg` | `IMG_20231225_143022.jpg` | Human-readable description |
| `Beach Vacation - Day 1.jpg` | `DSC_0001.jpg` | Descriptive context |
| `Sarah Birthday.jpg` | `Photo (3).jpg` | Meaningful name |

#### Detect Copy/Duplicate Patterns (Deprioritize)

- `Copy of *.jpg`
- `* (1).jpg`, `* (2).jpg`
- `*_copy.jpg`, `*_duplicate.jpg`
- `* - Copy.jpg`

#### Filename Merging

When duplicates have different names, combine information:

```
Input:
  /photos/IMG_20231225_143022.jpg  (preferred path)
  /temp/Christmas Dinner 2023.jpg  (better name)

Output:
  Keep: /photos/Christmas Dinner 2023.jpg
  Delete: /temp/Christmas Dinner 2023.jpg
```

### 3. Date-Based Selection

- **Prefer older files**: Original is usually created first
- **Match EXIF dates**: Prefer files where filesystem date matches EXIF capture date
- **Penalize future dates**: Files with dates in the future are likely incorrect

### 4. Metadata Completeness

Score files based on EXIF/metadata presence:

| Field | Weight |
|-------|--------|
| Camera Make/Model | +10 |
| GPS Coordinates | +15 |
| Original DateTime | +20 |
| Lens Info | +5 |
| Copyright | +5 |

Prefer files with higher metadata scores.

### 5. Quality/Size Analysis

- **Prefer larger file size**: Less compression, higher quality
- **Prefer higher resolution**: More pixels = better quality
- **Consider format**: RAW > TIFF > PNG > JPEG (when comparing same image)

## Scoring Algorithm

```
Total Score =
    (Path Priority × 3.0) +
    (Filename Score × 2.0) +
    (Metadata Score × 1.5) +
    (Date Score × 1.0) +
    (Quality Score × 1.0)
```

### Confidence Levels

| Score Difference | Confidence | Action |
|------------------|------------|--------|
| > 50 points | High | Auto-select |
| 20-50 points | Medium | Auto-select with flag |
| < 20 points | Low | Require manual review |

## API Design

### Auto-Select Endpoint

```http
POST /api/duplicates/{groupId}/auto-select
Content-Type: application/json

{
  "strategy": "smart",  // or "oldest", "largest", "path-priority"
  "dryRun": true,
  "renameToDescriptive": true
}
```

### Response

```json
{
  "groupId": "abc123",
  "selectedFileId": "file-001",
  "selectedFilePath": "/photos/Christmas Dinner 2023.jpg",
  "confidence": "high",
  "score": 85,
  "reasoning": [
    "Path priority: /photos (+100)",
    "Descriptive filename from duplicate (+20)",
    "Complete EXIF metadata (+35)",
    "Older creation date (+10)"
  ],
  "actions": [
    {
      "type": "rename",
      "from": "/photos/IMG_20231225_143022.jpg",
      "to": "/photos/Christmas Dinner 2023.jpg"
    },
    {
      "type": "delete",
      "path": "/temp/Christmas Dinner 2023.jpg"
    }
  ]
}
```

### Bulk Auto-Select

```http
POST /api/duplicates/auto-select-all
Content-Type: application/json

{
  "strategy": "smart",
  "minConfidence": "medium",
  "dryRun": true,
  "renameToDescriptive": true
}
```

## Database Schema Changes

### New Table: SelectionRules

```sql
CREATE TABLE selection_rules (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    rule_type VARCHAR(50) NOT NULL,  -- 'path_priority', 'filename_pattern', etc.
    pattern VARCHAR(500),
    priority INT NOT NULL DEFAULT 50,
    action VARCHAR(50),  -- 'prefer', 'deprioritize', 'require_review'
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### DuplicateGroups Table Additions

```sql
ALTER TABLE duplicate_groups ADD COLUMN
    auto_selection_score INT,
    auto_selection_confidence VARCHAR(20),
    auto_selection_reasoning JSONB,
    suggested_original_id UUID REFERENCES indexed_files(id),
    suggested_rename VARCHAR(500);
```

## Implementation Phases

### Phase 1: Basic Path Priority
- Implement path-based scoring
- Add configuration for path priorities
- Auto-select based on path alone

### Phase 2: Filename Analysis
- Detect copy/duplicate patterns
- Score descriptive vs generic names
- Implement filename merging logic

### Phase 3: Metadata & Quality
- Parse and score EXIF metadata
- Compare file sizes and resolutions
- Combine all scores

### Phase 4: Rename Operations
- Implement safe rename with rollback
- Handle filename conflicts
- Preserve original name in metadata/log

### Phase 5: Bulk Operations
- Batch processing with progress tracking
- Confidence thresholds for automation
- Review queue for low-confidence selections

## UI Requirements

### Duplicate Group List
- Show suggested action for each group
- Display confidence indicator (green/yellow/red)
- Quick-apply button for high-confidence selections

### Duplicate Detail View
- Show scoring breakdown for each file
- Highlight why one file was preferred
- Allow manual override with reason

### Settings Page
- Configure path priorities
- Enable/disable heuristics
- Set confidence thresholds

## Testing Requirements

- Unit tests for each scoring algorithm
- Integration tests with sample duplicate sets
- Edge cases: identical scores, missing metadata, special characters in filenames
- Performance tests with large duplicate groups (100+ files)

## Security Considerations

- Validate all file paths to prevent directory traversal
- Log all rename/delete operations for audit
- Implement dry-run mode for all destructive operations
- Require explicit confirmation for bulk operations

## Dependencies

- Existing DuplicateService and CleanerService
- EXIF metadata extraction (already implemented in IndexingService)
- File rename capabilities in CleanerService

## Acceptance Criteria

1. System correctly identifies preferred file in 90%+ of test cases
2. Filename merging preserves most descriptive name
3. All operations are logged and reversible
4. Bulk operations complete within reasonable time (< 1 min for 1000 groups)
5. UI clearly shows reasoning for each selection
