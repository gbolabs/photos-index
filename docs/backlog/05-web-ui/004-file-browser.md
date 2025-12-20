# 004: File Browser Component

**Priority**: P2 (User Interface)
**Agent**: A4
**Branch**: `feature/web-file-browser`
**Estimated Complexity**: High

## Objective

Implement a paginated file browser with search, filtering, and thumbnail gallery view.

## Dependencies

- `05-web-ui/001-api-services.md`

## Acceptance Criteria

- [ ] Display files in grid (gallery) and list views
- [ ] Lazy-loading thumbnails
- [ ] Pagination with page size options
- [ ] Filter by directory, date range, has duplicates
- [ ] Search by file name
- [ ] Sort by name, date, size
- [ ] File details panel on selection
- [ ] Responsive grid layout
- [ ] Virtual scrolling for large datasets
- [ ] Keyboard navigation

## TDD Steps

### Red Phase
```typescript
describe('FileBrowserComponent', () => {
  it('should display files in grid view', () => {});
  it('should paginate results', () => {});
  it('should filter by directory', () => {});
  it('should search by file name', () => {});
  it('should show file details on click', () => {});
});

describe('FileGridComponent', () => {
  it('should lazy load thumbnails', () => {});
  it('should handle missing thumbnails', () => {});
});

describe('FileFiltersComponent', () => {
  it('should emit filter changes', () => {});
  it('should reset filters', () => {});
});
```

### Green Phase
Implement components.

### Refactor Phase
Add virtual scrolling, optimize rendering.

## Files to Create

```
src/Web/src/app/features/files/
├── files.component.ts
├── files.component.html
├── files.component.scss
├── files.component.spec.ts
├── components/
│   ├── file-grid/
│   │   ├── file-grid.component.ts
│   │   ├── file-grid.component.html
│   │   └── file-grid.component.scss
│   ├── file-list/
│   │   ├── file-list.component.ts
│   │   └── file-list.component.html
│   ├── file-card/
│   │   ├── file-card.component.ts
│   │   └── file-card.component.scss
│   ├── file-filters/
│   │   ├── file-filters.component.ts
│   │   └── file-filters.component.html
│   ├── file-details-panel/
│   │   ├── file-details-panel.component.ts
│   │   └── file-details-panel.component.html
│   └── pagination/
│       ├── pagination.component.ts
│       └── pagination.component.html
├── models/
│   └── file-query.model.ts
└── files.routes.ts
```

## Component Implementation

```typescript
// files.component.ts
@Component({
  selector: 'app-files',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonToggleModule,
    FileGridComponent,
    FileListComponent,
    FileFiltersComponent,
    FileDetailsPanelComponent,
    PaginationComponent
  ],
  templateUrl: './files.component.html'
})
export class FilesComponent implements OnInit {
  files = signal<IndexedFileDto[]>([]);
  selectedFile = signal<IndexedFileDto | null>(null);
  loading = signal(true);
  viewMode = signal<'grid' | 'list'>('grid');
  pagination = signal({ page: 1, pageSize: 50, totalItems: 0, totalPages: 0 });

  query = signal<FileQueryParameters>({
    page: 1,
    pageSize: 50,
    sortBy: 'indexed',
    sortDesc: true
  });

  constructor(private fileService: IndexedFileService) {}

  ngOnInit(): void {
    this.loadFiles();
  }

  loadFiles(): void {
    this.loading.set(true);
    this.fileService.query(this.query()).subscribe({
      next: (response) => {
        this.files.set(response.items);
        this.pagination.set({
          page: response.page,
          pageSize: response.pageSize,
          totalItems: response.totalItems,
          totalPages: response.totalPages
        });
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  onFiltersChanged(filters: Partial<FileQueryParameters>): void {
    this.query.update(q => ({ ...q, ...filters, page: 1 }));
    this.loadFiles();
  }

  onPageChanged(page: number): void {
    this.query.update(q => ({ ...q, page }));
    this.loadFiles();
  }

  onFileSelected(file: IndexedFileDto): void {
    this.selectedFile.set(file);
  }

  onCloseDetails(): void {
    this.selectedFile.set(null);
  }

  toggleView(mode: 'grid' | 'list'): void {
    this.viewMode.set(mode);
  }
}
```

## File Grid with Lazy Thumbnails

```typescript
// file-grid.component.ts
@Component({
  selector: 'app-file-grid',
  standalone: true,
  imports: [CommonModule, FileCardComponent, ScrollingModule],
  template: `
    <cdk-virtual-scroll-viewport itemSize="220" class="file-grid-viewport">
      <div class="file-grid">
        @for (file of files(); track file.id) {
          <app-file-card
            [file]="file"
            [selected]="selectedId() === file.id"
            (select)="onSelect.emit($event)">
          </app-file-card>
        }
      </div>
    </cdk-virtual-scroll-viewport>
  `
})
export class FileGridComponent {
  files = input.required<IndexedFileDto[]>();
  selectedId = input<string | null>(null);
  onSelect = output<IndexedFileDto>();
}

// file-card.component.ts
@Component({
  selector: 'app-file-card',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatIconModule],
  template: `
    <mat-card
      [class.selected]="selected()"
      (click)="onSelect.emit(file())">
      <div class="thumbnail-container">
        @if (thumbnailLoaded()) {
          <img [src]="thumbnailUrl()" [alt]="file().fileName" loading="lazy">
        } @else {
          <mat-icon class="placeholder">image</mat-icon>
        }
        @if (file().duplicateGroupId) {
          <mat-icon class="duplicate-badge">content_copy</mat-icon>
        }
      </div>
      <mat-card-content>
        <p class="file-name" [title]="file().fileName">{{ file().fileName }}</p>
        <p class="file-size">{{ file().fileSizeBytes | fileSize }}</p>
      </mat-card-content>
    </mat-card>
  `
})
export class FileCardComponent implements OnInit {
  file = input.required<IndexedFileDto>();
  selected = input(false);
  onSelect = output<IndexedFileDto>();

  thumbnailUrl = computed(() => `/api/files/${this.file().id}/thumbnail`);
  thumbnailLoaded = signal(false);

  ngOnInit(): void {
    // Intersection observer for lazy loading
    this.loadThumbnail();
  }

  private loadThumbnail(): void {
    const img = new Image();
    img.onload = () => this.thumbnailLoaded.set(true);
    img.onerror = () => this.thumbnailLoaded.set(false);
    img.src = this.thumbnailUrl();
  }
}
```

## File Filters

```typescript
// file-filters.component.ts
@Component({
  selector: 'app-file-filters',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatCheckboxModule
  ],
  template: `
    <div class="filters-bar">
      <mat-form-field appearance="outline" class="search-field">
        <mat-label>Search</mat-label>
        <input matInput [formControl]="searchControl" placeholder="File name...">
        <mat-icon matPrefix>search</mat-icon>
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>Directory</mat-label>
        <mat-select [formControl]="directoryControl">
          <mat-option [value]="null">All directories</mat-option>
          @for (dir of directories(); track dir.id) {
            <mat-option [value]="dir.id">{{ dir.path }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-checkbox [formControl]="duplicatesOnlyControl">
        Duplicates only
      </mat-checkbox>

      <mat-form-field appearance="outline">
        <mat-label>Sort by</mat-label>
        <mat-select [formControl]="sortControl">
          <mat-option value="indexed">Date indexed</mat-option>
          <mat-option value="date">Date taken</mat-option>
          <mat-option value="name">File name</mat-option>
          <mat-option value="size">File size</mat-option>
        </mat-select>
      </mat-form-field>

      <button mat-stroked-button (click)="reset()">
        <mat-icon>clear</mat-icon>
        Reset
      </button>
    </div>
  `
})
export class FileFiltersComponent implements OnInit {
  directories = input<ScanDirectoryDto[]>([]);
  filtersChanged = output<Partial<FileQueryParameters>>();

  searchControl = new FormControl('');
  directoryControl = new FormControl<string | null>(null);
  duplicatesOnlyControl = new FormControl(false);
  sortControl = new FormControl('indexed');

  ngOnInit(): void {
    // Debounce search input
    this.searchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe(search => this.emitFilters());

    // Immediate for other controls
    merge(
      this.directoryControl.valueChanges,
      this.duplicatesOnlyControl.valueChanges,
      this.sortControl.valueChanges
    ).subscribe(() => this.emitFilters());
  }

  private emitFilters(): void {
    this.filtersChanged.emit({
      search: this.searchControl.value || undefined,
      directoryId: this.directoryControl.value || undefined,
      hasDuplicates: this.duplicatesOnlyControl.value || undefined,
      sortBy: this.sortControl.value || 'indexed'
    });
  }

  reset(): void {
    this.searchControl.reset('');
    this.directoryControl.reset(null);
    this.duplicatesOnlyControl.reset(false);
    this.sortControl.reset('indexed');
  }
}
```

## Test Coverage

- FilesComponent: 80% minimum
- FileGridComponent: 85% minimum
- FileCardComponent: 85% minimum
- FileFiltersComponent: 90% minimum
- Pagination: 90% minimum

## Completion Checklist

- [ ] Create FilesComponent as container
- [ ] Create FileGridComponent with virtual scroll
- [ ] Create FileCardComponent with lazy thumbnails
- [ ] Create FileListComponent (table view)
- [ ] Create FileFiltersComponent with debounced search
- [ ] Create FileDetailsPanelComponent for side panel
- [ ] Create PaginationComponent
- [ ] Implement view toggle (grid/list)
- [ ] Add keyboard navigation
- [ ] Optimize thumbnail loading
- [ ] Write unit tests for all components
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
