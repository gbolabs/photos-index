# 008: Hidden Files Feature

**Status**: 🔲 Not Started
**Priority**: P2 (User Experience)
**Agent**: A4
**Branch**: `feature/hidden-files`
**Estimated Complexity**: Medium-High

## Objective

Add ability to hide files/folders from the index without deleting them. Hidden files remain in the database but are filtered out by default, with a toggle to reveal them.

## Requirements

1. **Files stay indexed but hidden** - No data loss, just visibility control
2. **Toggle in all views** - Files, Gallery, Duplicates views all respect hidden state
3. **Multiple hide methods**:
   - Single file via context action
   - Multi-select files via bulk action
   - Folder path rule (hides all files under that path)
4. **Folder rules work on any path** - Even paths without direct images
5. **Category-based** - Extensible for future hide reasons
6. **Auto-hide new files** - Files indexed under hidden folder rules are automatically hidden

## Dependencies

- `01-shared-contracts/001-dtos.md`
- `02-api-layer/002-indexed-files.md`

## Database Schema

### New Entity: `HiddenFolder`

```csharp
public class HiddenFolder
{
    public Guid Id { get; set; }
    public required string FolderPath { get; set; }  // e.g., "/volume1/Photos/Screenshots"
    public string? Description { get; set; }          // Optional user note
    public DateTime CreatedAt { get; set; }
}
```

### New Fields on `IndexedFile`

```csharp
public bool IsHidden { get; set; } = false;
public HiddenCategory? HiddenCategory { get; set; }   // Manual, FolderRule, etc.
public DateTime? HiddenAt { get; set; }
public Guid? HiddenByFolderId { get; set; }           // FK to HiddenFolder
public HiddenFolder? HiddenByFolder { get; set; }     // Navigation property
```

### New Enum: `HiddenCategory`

```csharp
public enum HiddenCategory
{
    Manual,      // User explicitly hid this file
    FolderRule   // Hidden due to folder path rule
    // Future: FileType, Extension, Size, etc.
}
```

## API Design

### New Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/api/files/hide` | Hide files by IDs (Manual category) |
| POST | `/api/files/unhide` | Unhide files by IDs |
| GET | `/api/files/folders` | Get unique folder paths for autocomplete |
| GET | `/api/hidden-folders` | List all hidden folder rules |
| POST | `/api/hidden-folders` | Create hidden folder rule |
| DELETE | `/api/hidden-folders/{id}` | Remove folder rule (unhides files) |

### Updated Endpoints

| Endpoint | Change |
|----------|--------|
| `GET /api/files` | Add `includeHidden` query param (default: false) |
| `GET /api/duplicates` | Add `includeHidden` query param (default: false) |

### Request/Response DTOs

```csharp
public record HideFilesRequest
{
    public required List<Guid> FileIds { get; init; }
}

public record CreateHiddenFolderRequest
{
    public required string FolderPath { get; init; }
    public string? Description { get; init; }
}

public record HiddenFolderDto
{
    public Guid Id { get; init; }
    public string FolderPath { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public int AffectedFileCount { get; init; }
}

public record FolderPathDto
{
    public string Path { get; init; }
    public int FileCount { get; init; }
}
```

## UI Design

### 1. Global Toggle (All Views)

**Location**: Top toolbar/filter bar in Files, Gallery, Duplicates

```
[Show Hidden (42)] toggle button
```

- Shows count of hidden files when off
- When ON: hidden files appear with visual indicator (opacity, icon overlay)
- State persisted in localStorage

### 2. Hide Action - Single File

**Location**: File row actions (Files page), Tile hover menu (Gallery)

```
[...] → Hide from view
```

### 3. Hide Action - Multi-Select

**Location**: Bulk actions toolbar (appears when files selected)

```
[X selected] [Hide Selected] [Delete Selected]
```

### 4. Hide Action - Folder Path (Settings Page)

**Location**: Settings page → "Hidden Folders" section

```
+------------------------------------------+
| Hidden Folders                           |
+------------------------------------------+
| Path                    | Files | Action |
|-------------------------|-------|--------|
| /volume1/Photos/GIFs    |   234 | [x]    |
| /volume1/Public/Code    |    56 | [x]    |
+------------------------------------------+
| [+ Add Folder Path]                      |
+------------------------------------------+
```

**Add Folder Dialog** (with autocomplete from indexed paths):
```
+----------------------------------+
| Hide Folder                      |
+----------------------------------+
| Folder Path:                     |
| [/volume1/Photos/____________]   |  ← Autocomplete
|                                  |
| Description (optional):          |
| [GIF collection_______________]  |
|                                  |
| Files that will be hidden: 234   |
|                                  |
| [Cancel]            [Hide Folder]|
+----------------------------------+
```

### 5. Visual Indicators for Hidden Files

When "Show Hidden" is ON:
- **Opacity**: 50% opacity on thumbnails/rows
- **Badge**: Small "hidden" icon overlay (visibility_off)
- **Chip**: "Hidden" chip in file details

## Files to Create/Modify

### Backend

| File | Action |
|------|--------|
| `src/Database/Entities/HiddenFolder.cs` | Create |
| `src/Database/Entities/IndexedFile.cs` | Modify (add fields) |
| `src/Database/Entities/HiddenCategory.cs` | Create |
| `src/Database/PhotosDbContext.cs` | Add DbSet, configure |
| `src/Database/Migrations/xxx_AddHiddenFilesFeature.cs` | Generate |
| `src/Shared/Dtos/HiddenFolderDto.cs` | Create |
| `src/Shared/Dtos/IndexedFileDto.cs` | Add IsHidden, HiddenCategory |
| `src/Shared/Requests/FileQueryParameters.cs` | Add IncludeHidden |
| `src/Shared/Requests/HideFilesRequest.cs` | Create |
| `src/Api/Controllers/HiddenFoldersController.cs` | Create |
| `src/Api/Controllers/IndexedFilesController.cs` | Add hide/unhide, folders endpoints |
| `src/Api/Services/IndexedFileService.cs` | Update query filtering |
| `src/Api/Services/HiddenFolderService.cs` | Create |
| `src/Api/Services/DuplicateService.cs` | Update to filter hidden |
| `src/Api/Services/FileIngestService.cs` | Auto-hide new files matching folder rules |

### Frontend

| File | Action |
|------|--------|
| `src/Web/src/app/models/hidden-folder.model.ts` | Create |
| `src/Web/src/app/models/indexed-file.model.ts` | Add IsHidden |
| `src/Web/src/app/services/hidden-folder.service.ts` | Create |
| `src/Web/src/app/services/indexed-file.service.ts` | Add hide/unhide methods |
| `src/Web/src/app/features/gallery/components/filter-bar/` | Add toggle |
| `src/Web/src/app/features/files/files.ts` | Add toggle, hide action |
| `src/Web/src/app/features/duplicates/duplicates.ts` | Add toggle |
| `src/Web/src/app/features/settings/settings.ts` | Add hidden folders section |
| `src/Web/src/app/features/settings/components/hidden-folders/` | Create component |
| `src/Web/src/app/shared/components/hide-folder-dialog/` | Create dialog |

## Query Logic

### Files Query (IndexedFileService.cs)

```csharp
// Default: exclude hidden files
if (!query.IncludeHidden)
{
    queryable = queryable.Where(f => !f.IsHidden);
}
```

### When Creating Folder Rule

```csharp
var affectedFiles = await _db.IndexedFiles
    .Where(f => f.FilePath.StartsWith(folderPath))
    .ToListAsync();

foreach (var file in affectedFiles)
{
    file.IsHidden = true;
    file.HiddenCategory = HiddenCategory.FolderRule;
    file.HiddenAt = DateTime.UtcNow;
    file.HiddenByFolderId = hiddenFolder.Id;
}
```

### When Removing Folder Rule

```csharp
var affectedFiles = await _db.IndexedFiles
    .Where(f => f.HiddenByFolderId == folderId)
    .ToListAsync();

foreach (var file in affectedFiles)
{
    file.IsHidden = false;
    file.HiddenCategory = null;
    file.HiddenAt = null;
    file.HiddenByFolderId = null;
}
```

### Auto-Hide During Indexing

In `FileIngestService.cs` when creating new IndexedFile:

```csharp
var matchingRule = await _db.HiddenFolders
    .FirstOrDefaultAsync(hf => filePath.StartsWith(hf.FolderPath), ct);

if (matchingRule != null)
{
    indexedFile.IsHidden = true;
    indexedFile.HiddenCategory = HiddenCategory.FolderRule;
    indexedFile.HiddenAt = DateTime.UtcNow;
    indexedFile.HiddenByFolderId = matchingRule.Id;
}
```

## Test Coverage

- HiddenFolderService: 80% minimum
- IndexedFileService (hidden filtering): 80% minimum
- API endpoints: Integration tests

## Acceptance Criteria

- [ ] Files can be hidden individually via UI action
- [ ] Files can be hidden via multi-select bulk action
- [ ] Folder paths can be added as hidden folder rules
- [ ] Hidden folder rules use autocomplete from indexed paths
- [ ] All views (Files, Gallery, Duplicates) have "Show Hidden" toggle
- [ ] Hidden files show visual indicator when toggle is ON
- [ ] New files matching folder rules are auto-hidden during indexing
- [ ] Removing a folder rule unhides all files hidden by that rule
- [ ] Hidden files count shown in toggle button
- [ ] Hidden state persisted in localStorage

## Completion Checklist

- [ ] Create HiddenFolder entity
- [ ] Add hidden fields to IndexedFile entity
- [ ] Create HiddenCategory enum
- [ ] Generate EF Core migration
- [ ] Create HiddenFolderDto and related DTOs
- [ ] Add IncludeHidden to FileQueryParameters
- [ ] Create HiddenFoldersController
- [ ] Add hide/unhide endpoints to IndexedFilesController
- [ ] Add folders autocomplete endpoint
- [ ] Update IndexedFileService query filtering
- [ ] Create HiddenFolderService
- [ ] Update DuplicateService to filter hidden
- [ ] Update FileIngestService for auto-hide
- [ ] Create Angular hidden-folder.service.ts
- [ ] Update indexed-file.service.ts
- [ ] Add toggle to filter-bar component
- [ ] Add toggle to files page
- [ ] Add toggle to duplicates page
- [ ] Create hidden-folders settings component
- [ ] Create hide-folder-dialog component
- [ ] Add hide action to file row/tile
- [ ] Add bulk hide action
- [ ] Write unit tests
- [ ] Write integration tests
- [ ] PR created and reviewed
