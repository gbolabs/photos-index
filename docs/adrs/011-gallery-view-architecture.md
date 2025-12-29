# ADR 011: Gallery View Architecture

## Status
Proposed

## Date
2025-12-29

## Context

Issue #126 requests a gallery view that:
- Displays photos in a grid layout (like Google Photos, Apple Photos)
- Supports infinite scroll for large collections (100k+ files)
- Allows filtering by folder, date, and future criteria (tags, location, smart albums)
- Includes a date/time navigation sidebar
- Shows a detail pane when clicking on a tile
- Should be reusable across different contexts (collection, folder, date, etc.)

The current Files view uses a paginated table which is not ideal for browsing photos visually.

## Decision

### 1. Virtual Scrolling with Angular CDK

Use `@angular/cdk/scrolling` with `cdk-virtual-scroll-viewport` for efficient rendering of large photo collections.

**Rationale:**
- Already included in project dependencies (Angular CDK 21.0.5)
- Only renders visible items, enabling smooth scrolling with 100k+ items
- Native Angular solution, well-maintained
- Works with CSS Grid for responsive layouts

**Alternative Considered:**
- `ngx-infinite-scroll` - Simpler but loads all items into DOM, poor performance with large datasets

### 2. Responsive Grid Layout

Use CSS Grid with `auto-fill` for responsive columns that adapt to viewport width.

```scss
.gallery-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  gap: 4px;
}
```

**Tile Sizes (configurable):**
- Small: 100px (more tiles, less detail)
- Medium: 150px (default, balanced)
- Large: 200px (fewer tiles, more detail)

### 3. Component Architecture

```
src/app/features/gallery/
├── gallery.ts                    # Main gallery component (route)
├── gallery.html
├── gallery.scss
├── components/
│   ├── gallery-grid/             # Virtual scroll grid
│   │   ├── gallery-grid.ts
│   │   ├── gallery-grid.html
│   │   └── gallery-grid.scss
│   ├── gallery-tile/             # Individual photo tile
│   │   ├── gallery-tile.ts
│   │   ├── gallery-tile.html
│   │   └── gallery-tile.scss
│   ├── date-navigator/           # Date sidebar/dropdown
│   │   ├── date-navigator.ts
│   │   └── ...
│   └── filter-bar/               # Quick filters (folder, date range)
│       └── ...
└── services/
    └── gallery-state.service.ts  # Centralized state management
```

### 4. State Management with Signals

Use Angular signals for reactive state, consistent with existing codebase:

```typescript
@Injectable({ providedIn: 'root' })
export class GalleryStateService {
  // View state
  readonly viewMode = signal<'grid' | 'timeline'>('grid');
  readonly tileSize = signal<'small' | 'medium' | 'large'>('medium');

  // Data state
  readonly files = signal<IndexedFileDto[]>([]);
  readonly loading = signal(false);
  readonly hasMore = signal(true);

  // Filter state
  readonly filters = signal<GalleryFilters>({
    folderId: null,
    dateRange: null,
    search: null
  });

  // Computed
  readonly groupedByDate = computed(() =>
    this.groupFilesByDate(this.files())
  );
}
```

### 5. Data Loading Strategy

**Hybrid Approach: Virtual Scroll + Incremental Loading**

1. Load initial batch (e.g., 200 items) on component init
2. Virtual scroll renders only visible items (~20-50 depending on viewport)
3. Detect when user scrolls near the end of loaded data
4. Fetch next batch from API (append to existing)
5. Continue until no more items

**API Changes Required:**
- Add cursor-based pagination option for efficient "load more"
- Or use existing offset pagination with larger page sizes

### 6. Date Navigation

**Two modes:**
1. **Sidebar (desktop):** Scrollable list of year/month with counts
2. **Dropdown (mobile):** Compact date picker with jump-to-date

Clicking a date scrolls the gallery to that position (via virtual scroll's `scrollToIndex`).

### 7. Reusability

The `GalleryGridComponent` accepts files as input, making it reusable:

```typescript
@Component({...})
export class GalleryGridComponent {
  files = input.required<IndexedFileDto[]>();
  loading = input(false);
  hasMore = input(true);

  loadMore = output<void>();
  fileClick = output<IndexedFileDto>();
}
```

**Usage contexts:**
- `/gallery` - Full collection
- `/gallery?folder=xxx` - Folder view
- `/gallery?date=2024-12` - Date view
- Embedded in Duplicates view for group preview

### 8. Detail Pane Integration

Reuse existing `ImagePreviewModalComponent` for now. Future enhancement could add:
- Slide-out panel instead of modal
- Swipe gestures for next/previous
- EXIF metadata display

## Consequences

### Positive
- Smooth scrolling with 100k+ photos
- Responsive design works on all devices
- Reusable components for future features
- Consistent with existing codebase patterns
- No new dependencies required

### Negative
- Virtual scroll requires fixed item heights (or height estimation)
- Date grouping with virtual scroll is complex (need custom implementation)
- Initial development effort higher than simple pagination

### Risks
- Performance with very large datasets (500k+ files) - mitigated by server-side pagination
- Memory usage if user scrolls through entire collection - mitigated by virtual scroll

## Implementation Plan

### Phase 1: Basic Gallery Grid (MVP)
1. Create gallery feature module with routing
2. Implement `GalleryGridComponent` with virtual scroll
3. Implement `GalleryTileComponent` with thumbnail loading
4. Add basic filtering (folder, search)
5. Integrate with existing file service

### Phase 2: Date Navigation
1. Add date grouping logic
2. Implement `DateNavigatorComponent`
3. Jump-to-date functionality

### Phase 3: Enhancements
1. Tile size selector
2. Keyboard navigation
3. Multi-select for batch operations
4. Timeline view alternative

## References

- [Angular CDK Virtual Scrolling](https://material.angular.dev/cdk/scrolling/overview)
- [Issue #126](https://github.com/gbolabs/photos-index/issues/126)
- [Backlog Task 05-004](../backlog/05-web-ui/004-file-browser.md)
