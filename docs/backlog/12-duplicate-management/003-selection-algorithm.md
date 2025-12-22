# 003: Smart Selection Algorithm

**Status**: 🔲 Not Started
**Priority**: P2
**Issue**: [#68](https://github.com/gbolabs/photos-index/issues/68)
**Branch**: `feature/selection-algorithm`
**Estimated Complexity**: Medium
**Target Release**: v0.3.0

## Objective

Implement a configurable scoring algorithm to automatically select the "original to keep" based on path priorities, EXIF data, file age, and other factors.

## Dependencies

- `12-002` Batch Validation (status field)

## Acceptance Criteria

- [ ] Path priority configuration in Settings
- [ ] Scoring algorithm considers: path priority, depth, EXIF, age
- [ ] "Recalculate Originals" button to re-run algorithm
- [ ] Preview mode: show what would change before applying
- [ ] Default Synology-friendly priorities (/photos > /public)
- [ ] Save preferences per user/session
- [ ] Visual indicator when original was auto-selected vs manually chosen
- [ ] Conflict detection: Mark groups orange when algorithm can't decide (e.g., ties)
- [ ] Auto-run algorithm when duplicate group created or file added to existing group
- [ ] Re-run algorithm and invalidate selections when preferences change

## Technical Design

### Scoring Algorithm

```typescript
function calculateScore(file: IndexedFile, config: SelectionConfig): number {
  let score = 0;

  // 1. Path priority (user-configured)
  const pathMatch = config.pathPriorities.find(p => file.filePath.startsWith(p.prefix));
  score += pathMatch?.score ?? 0;

  // 2. Path depth (deeper = more organized)
  const depth = file.filePath.split('/').filter(Boolean).length;
  score += Math.min(depth, 5) * 5;  // Max +25

  // 3. EXIF data presence
  if (file.dateTaken || file.cameraMake) score += 20;

  // 4. File age (older indexed = established)
  const ageMonths = monthsSince(file.indexedAt);
  score += Math.min(ageMonths, 12);  // Max +12

  // 5. Tiebreaker: shorter path
  score -= file.filePath.length * 0.01;

  return score;
}

// Conflict detection
function selectOriginal(group: DuplicateGroup, config: SelectionConfig): SelectionResult {
  const scores = group.files.map(f => ({ file: f, score: calculateScore(f, config) }));
  scores.sort((a, b) => b.score - a.score);

  const topScore = scores[0].score;
  const runnerUp = scores[1]?.score ?? 0;

  // If scores are too close (within 5 points), mark as conflict
  if (topScore - runnerUp < 5) {
    return { status: 'conflict', file: null };  // Orange - needs manual selection
  }

  return { status: 'auto-selected', file: scores[0].file };  // Green
}
```

### DuplicateGroup Status Values

```
pending      - New group, awaiting algorithm run
auto-selected - Algorithm chose an original (green)
conflict     - Algorithm couldn't decide, needs manual (orange)
validated    - User confirmed selection (purple)
cleaned      - Duplicates removed by Cleaner Service
```

### Algorithm Trigger Points

The selection algorithm runs automatically:
1. When Indexing Service creates a new duplicate group
2. When a file is added to an existing duplicate group
3. When user changes preferences and clicks "Recalculate"

### Configuration Entity

```csharp
public class SelectionPreference
{
    public Guid Id { get; set; }
    public string PathPrefix { get; set; }  // e.g., "/photos/"
    public int Priority { get; set; }       // 0-100
    public int SortOrder { get; set; }
}
```

### Settings UI

```
┌─────────────────────────────────────────────────────────────────┐
│ ⚙️ Original Selection Preferences                               │
├─────────────────────────────────────────────────────────────────┤
│ Directory Priority:                                             │
│   /photos/*     [████████████████████] 100  [↑][↓][✕]          │
│   /albums/*     [████████████████    ]  80  [↑][↓][✕]          │
│   /public/*     [██                  ]  10  [↑][↓][✕]          │
│   [+ Add Rule]                                                  │
│                                                                 │
│ Additional factors:                                             │
│   [x] Prefer files with EXIF data (+20)                        │
│   [x] Prefer deeper folder structure (+5/level)                │
│   [x] Prefer older files (+1/month, max 12)                    │
│                                                                 │
│ [Save] [Reset to Defaults] [Recalculate All Pending]           │
└─────────────────────────────────────────────────────────────────┘
```

### API Endpoints

```
GET /api/selection-preferences
POST /api/selection-preferences
Body: { preferences: SelectionPreference[] }

POST /api/duplicate-groups/recalculate-originals
Body: { scope: 'pending' | 'all' }
Response: { updated: number, preview?: DuplicateGroupDto[] }
```

## Files to Create/Modify

### Backend
- `src/Database/Entities/SelectionPreference.cs` (new)
- `src/Api/Controllers/SelectionPreferencesController.cs` (new)
- `src/Api/Services/OriginalSelectionService.cs` (new)
- `src/Shared/Dtos/SelectionPreferenceDto.cs` (new)

### Frontend
- `src/Web/src/app/features/settings/components/selection-preferences/` (new)
- `src/Web/src/app/services/selection-preference.service.ts` (new)

## Default Priorities

```
/photos/*           100   (Synology Photos organized)
/family/*            70   (Shared family albums)
/albums/*            60   (Manual albums)
/public/*            10   (Raw dumps, unsorted backups)
/backup/*             5   (Backup folders)
```

Note: `/home` and `/homes` paths not currently indexed.

## Test Coverage

- Unit tests for scoring algorithm
- Test priority ordering
- Test conflict detection (close scores → orange)
- Test recalculation across groups
- Test algorithm trigger on group creation/update
- Settings persistence tests
