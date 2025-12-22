# 006: File Detail Page with Extended Metadata

## Problem Statement

Current issues with file viewing:
1. **No detail view**: Clicking a file shows limited info in a list/grid
2. **View button broken**: Eye icon to "view file" doesn't work - API has no access to original files (they're on Synology NAS)
3. **Limited metadata display**: Only basic columns (name, size, date) shown
4. **No navigation**: Can't navigate back after clicking view

## Proposed Solution

Create a proper file detail page with:
1. Full metadata display
2. Thumbnail preview (when available)
3. Proper navigation (back button, breadcrumbs)
4. Remove or repurpose the broken "view file" button

## Current Architecture Constraint

```
┌─────────────────┐          ┌─────────────────┐
│   Synology NAS  │          │  MPC (TrueNAS)  │
│                 │          │                 │
│  Original Files │ ──────── │  API + Database │
│  (not exposed)  │  1Gbit/s │  (thumbnails    │
│                 │          │   only)         │
└─────────────────┘          └─────────────────┘
                                     │
                                     ▼
                             ┌─────────────────┐
                             │   Web Browser   │
                             │                 │
                             │  Can only see:  │
                             │  - Metadata     │
                             │  - Thumbnails   │
                             │  - File paths   │
                             └─────────────────┘
```

**Key constraint**: The API cannot serve original files - they exist only on the Synology NAS which is not directly accessible from the web.

## UI Design

### File List View (Enhanced)

Add more columns to the file list:

| Column | Source | Notes |
|--------|--------|-------|
| Thumbnail | `GET /api/files/{id}/thumbnail` | Small preview |
| File Name | `fileName` | Existing |
| Path | `filePath` | Directory location |
| Size | `fileSize` | Human-readable (KB, MB) |
| Dimensions | `width` x `height` | e.g., "4032 x 3024" |
| Date Taken | `dateTaken` | From EXIF |
| Modified | `modifiedAt` | File system date |
| Camera | `cameraMake` + `cameraModel` | e.g., "Apple iPhone 14 Pro" |
| Duplicate | `isDuplicate` | Badge/icon |

### File Detail Page

Route: `/files/:id`

```
┌────────────────────────────────────────────────────────────────────┐
│  ← Back to Files                                    [Copy Path]    │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  ┌──────────────────────┐   File Information                       │
│  │                      │   ─────────────────                      │
│  │                      │   Name:       IMG_1234.HEIC              │
│  │     [Thumbnail]      │   Path:       /photos/2024/January/      │
│  │      (200x200)       │   Size:       4.2 MB                     │
│  │                      │   Dimensions: 4032 x 3024                │
│  │                      │   Hash:       a1b2c3d4...                │
│  └──────────────────────┘                                          │
│                             EXIF Metadata                          │
│                             ─────────────                          │
│                             Date Taken:   2024-01-15 14:32:00      │
│                             Camera:       Apple iPhone 14 Pro      │
│                             Lens:         iPhone 14 Pro back...    │
│                             ISO:          50                       │
│                             Aperture:     f/1.78                   │
│                             Shutter:      1/120s                   │
│                             Focal Length: 6.86mm                   │
│                                                                    │
│                             GPS Location                           │
│                             ────────────                           │
│                             Latitude:     48.8584° N               │
│                             Longitude:    2.2945° E                │
│                             [View on Map]                          │
│                                                                    │
│                             Duplicate Status                       │
│                             ────────────────                       │
│                             ⚠️ Part of duplicate group             │
│                             3 other files with same hash           │
│                             [View Duplicate Group]                 │
│                                                                    │
│                             File System                            │
│                             ───────────                            │
│                             Created:      2024-01-15 14:32:00      │
│                             Modified:     2024-01-15 14:32:00      │
│                             Indexed:      2024-12-20 10:15:00      │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

### Navigation

1. **From file list**: Click row or "details" button → Navigate to `/files/:id`
2. **Back button**: Returns to previous list with scroll position preserved
3. **Breadcrumbs**: `Files > path/to/directory > filename.jpg`
4. **Keyboard**: Arrow keys to navigate between files in same view

## API Enhancements

### Extended File Response

Current `IndexedFileResponse` needs additional fields:

```csharp
public record IndexedFileDetailResponse
{
    // Existing
    public Guid Id { get; init; }
    public string FilePath { get; init; }
    public string FileName { get; init; }
    public string FileHash { get; init; }
    public long FileSize { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime ModifiedAt { get; init; }
    public bool IsDuplicate { get; init; }
    public Guid? DuplicateGroupId { get; init; }
    public string? ThumbnailPath { get; init; }

    // New - EXIF Metadata
    public DateTime? DateTaken { get; init; }
    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }
    public string? LensModel { get; init; }
    public int? Iso { get; init; }
    public string? Aperture { get; init; }      // "f/1.78"
    public string? ShutterSpeed { get; init; }  // "1/120s"
    public string? FocalLength { get; init; }   // "6.86mm"
    public double? GpsLatitude { get; init; }
    public double? GpsLongitude { get; init; }
    public double? GpsAltitude { get; init; }
    public string? Orientation { get; init; }   // "Landscape", "Portrait"
    public string? ColorSpace { get; init; }    // "sRGB"

    // New - Computed
    public string FileSizeFormatted { get; init; }  // "4.2 MB"
    public string DimensionsFormatted { get; init; } // "4032 x 3024"
    public int? DuplicateCount { get; init; }        // Number of duplicates
    public bool HasThumbnail { get; init; }
}
```

### Database Schema Changes

Extend `IndexedFiles` table:

```sql
ALTER TABLE "IndexedFiles" ADD COLUMN "DateTaken" timestamptz NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "CameraMake" varchar(100) NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "CameraModel" varchar(100) NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "LensModel" varchar(200) NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "Iso" int NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "Aperture" varchar(20) NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "ShutterSpeed" varchar(20) NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "FocalLength" varchar(20) NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "GpsLatitude" double precision NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "GpsLongitude" double precision NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "GpsAltitude" double precision NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "Orientation" varchar(20) NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "ColorSpace" varchar(20) NULL;
```

### Indexing Service Changes

Update `MetadataExtractor` to extract and send these EXIF fields. Current implementation extracts some but doesn't persist all.

## Angular Components

### New Components

```
src/app/files/
├── file-list/
│   ├── file-list.component.ts       # Enhanced with more columns
│   └── file-list.component.html
├── file-detail/
│   ├── file-detail.component.ts     # NEW - Detail page
│   ├── file-detail.component.html
│   └── file-detail.component.scss
├── file-thumbnail/
│   ├── file-thumbnail.component.ts  # Reusable thumbnail display
│   └── file-thumbnail.component.html
└── file-metadata/
    ├── file-metadata.component.ts   # Reusable metadata section
    └── file-metadata.component.html
```

### Routes

```typescript
const routes: Routes = [
  { path: 'files', component: FileListComponent },
  { path: 'files/:id', component: FileDetailComponent },
];
```

### Services

```typescript
@Injectable({ providedIn: 'root' })
export class FileService {
  getFile(id: string): Observable<IndexedFileDetail> {
    return this.http.get<IndexedFileDetail>(`/api/files/${id}`);
  }

  getFiles(params: FileQueryParams): Observable<PagedResult<IndexedFileSummary>> {
    return this.http.get<PagedResult<IndexedFileSummary>>('/api/files', { params });
  }
}
```

## What to Do with "View File" Button

Options:

### Option A: Remove It
- Simplest solution
- Honest about architectural limitation
- Users can use the file path to access via SMB/NFS

### Option B: Copy Path to Clipboard
- Replace eye icon with copy icon
- One-click copy of full file path
- User can paste into file explorer
- Toast: "Path copied to clipboard"

### Option C: Generate SMB/NFS Link (Advanced)
- Configure base SMB path in settings
- Generate clickable `smb://nas/photos/path/file.jpg` link
- Works on macOS, may work on Windows
- Requires user to have NAS mounted

**Recommendation**: Option B (Copy Path) - practical and honest.

## Implementation Tasks

### Phase 1: Database & API
- [ ] Add EXIF columns to IndexedFiles entity
- [ ] Create EF Core migration
- [ ] Update MetadataExtractor to persist all EXIF fields
- [ ] Create `GET /api/files/{id}` detail endpoint
- [ ] Update batch ingest to include EXIF fields

### Phase 2: Angular File List
- [ ] Add thumbnail column with lazy loading
- [ ] Add dimensions column
- [ ] Add camera column
- [ ] Add date taken column
- [ ] Replace "view" button with "copy path" button
- [ ] Add click handler to navigate to detail page

### Phase 3: Angular Detail Page
- [ ] Create FileDetailComponent
- [ ] Create route `/files/:id`
- [ ] Display all metadata sections
- [ ] Add back navigation
- [ ] Add keyboard navigation (prev/next file)
- [ ] Integrate with duplicate group viewer

### Phase 4: Polish
- [ ] Loading states and skeletons
- [ ] Error handling for missing files
- [ ] Responsive design for mobile
- [ ] Add to file search results
- [ ] Deep linking support

## Migration Strategy

For existing indexed files without EXIF data:
1. Add columns as nullable
2. On next indexing cycle, extractor will populate new fields
3. Or: Create one-time re-extraction job for existing files

## Success Criteria

- [ ] File list shows thumbnail, dimensions, camera, date taken
- [ ] Clicking file navigates to detail page
- [ ] Detail page shows all available metadata
- [ ] Back navigation preserves list scroll position
- [ ] Copy path button works on all browsers
- [ ] GPS coordinates link to map service
- [ ] Duplicate status links to duplicate group
