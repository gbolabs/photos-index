# 005: Duplicate Viewer Component

**Status**: ✅ Complete
**PR**: [#10](https://github.com/gbolabs/photos-index/pull/10)
**Priority**: P2 (User Interface)
**Agent**: A4
**Branch**: `feature/web-duplicates`
**Estimated Complexity**: High

## Objective

Implement a dedicated view for managing duplicate groups with side-by-side comparison and bulk actions.

## Dependencies

- `05-web-ui/001-api-services.md`
- `05-web-ui/004-file-browser.md` (FileCardComponent reuse)

## Acceptance Criteria

- [ ] List all duplicate groups with counts
- [ ] Show all files in a group side-by-side
- [ ] Highlight current original file
- [ ] Select different file as original
- [ ] Auto-select original based on rules
- [ ] Delete non-originals with confirmation
- [ ] Bulk actions (auto-select all, delete all selected)
- [ ] Preview images at full size
- [ ] Show file metadata comparison
- [ ] Progress indicator for bulk operations

## TDD Steps

### Red Phase
```typescript
describe('DuplicatesComponent', () => {
  it('should list all duplicate groups', () => {});
  it('should show group details on select', () => {});
  it('should highlight original file', () => {});
});

describe('DuplicateGroupComponent', () => {
  it('should display all files in group', () => {});
  it('should allow setting original', () => {});
  it('should confirm before delete', () => {});
});

describe('ImageComparisonComponent', () => {
  it('should show images side by side', () => {});
  it('should sync zoom between images', () => {});
});
```

### Green Phase
Implement components.

### Refactor Phase
Add bulk operations, optimize rendering.

## Files to Create

```
src/Web/src/app/features/duplicates/
├── duplicates.component.ts
├── duplicates.component.html
├── duplicates.component.scss
├── duplicates.component.spec.ts
├── components/
│   ├── duplicate-group-list/
│   │   ├── duplicate-group-list.component.ts
│   │   └── duplicate-group-list.component.html
│   ├── duplicate-group-detail/
│   │   ├── duplicate-group-detail.component.ts
│   │   ├── duplicate-group-detail.component.html
│   │   └── duplicate-group-detail.component.scss
│   ├── image-comparison/
│   │   ├── image-comparison.component.ts
│   │   └── image-comparison.component.scss
│   ├── file-metadata-table/
│   │   ├── file-metadata-table.component.ts
│   │   └── file-metadata-table.component.html
│   └── bulk-actions-toolbar/
│       ├── bulk-actions-toolbar.component.ts
│       └── bulk-actions-toolbar.component.html
└── duplicates.routes.ts
```

## Component Implementation

```typescript
// duplicates.component.ts
@Component({
  selector: 'app-duplicates',
  standalone: true,
  imports: [
    CommonModule,
    DuplicateGroupListComponent,
    DuplicateGroupDetailComponent,
    BulkActionsToolbarComponent
  ],
  templateUrl: './duplicates.component.html'
})
export class DuplicatesComponent implements OnInit {
  groups = signal<DuplicateGroupDto[]>([]);
  selectedGroup = signal<DuplicateGroupDto | null>(null);
  loading = signal(true);
  pagination = signal({ page: 1, pageSize: 20, totalItems: 0 });
  bulkMode = signal(false);
  selectedGroupIds = signal<Set<string>>(new Set());

  constructor(
    private duplicateService: DuplicateService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadGroups();
  }

  loadGroups(): void {
    this.loading.set(true);
    this.duplicateService.getGroups(
      this.pagination().page,
      this.pagination().pageSize
    ).subscribe({
      next: (response) => {
        this.groups.set(response.items);
        this.pagination.update(p => ({
          ...p,
          totalItems: response.totalItems
        }));
        this.loading.set(false);
      }
    });
  }

  onSelectGroup(group: DuplicateGroupDto): void {
    this.duplicateService.getGroup(group.id).subscribe({
      next: (fullGroup) => this.selectedGroup.set(fullGroup)
    });
  }

  onSetOriginal(groupId: string, fileId: string): void {
    this.duplicateService.setOriginal(groupId, fileId).subscribe({
      next: () => {
        this.snackBar.open('Original set', 'OK', { duration: 2000 });
        this.onSelectGroup({ id: groupId } as DuplicateGroupDto);
      }
    });
  }

  onAutoSelect(groupId: string): void {
    this.duplicateService.autoSelectOriginal(groupId).subscribe({
      next: () => {
        this.snackBar.open('Original auto-selected', 'OK', { duration: 2000 });
        this.onSelectGroup({ id: groupId } as DuplicateGroupDto);
      }
    });
  }

  onDeleteNonOriginals(groupId: string): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete Duplicates',
        message: 'Move all non-original files to trash?',
        confirmText: 'Delete',
        confirmColor: 'warn'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.duplicateService.deleteNonOriginals(groupId).subscribe({
          next: (count) => {
            this.snackBar.open(`${count} files queued for deletion`, 'OK', { duration: 3000 });
            this.loadGroups();
          }
        });
      }
    });
  }

  // Bulk operations
  toggleBulkMode(): void {
    this.bulkMode.update(v => !v);
    if (!this.bulkMode()) {
      this.selectedGroupIds.set(new Set());
    }
  }

  onBulkAutoSelect(): void {
    const ids = Array.from(this.selectedGroupIds());
    this.duplicateService.autoSelectAll({ groupIds: ids }).subscribe({
      next: (count) => {
        this.snackBar.open(`Auto-selected originals for ${count} groups`, 'OK', { duration: 3000 });
        this.loadGroups();
      }
    });
  }

  onBulkDelete(): void {
    // Similar to single delete but with batch
  }
}
```

## Duplicate Group Detail

```typescript
// duplicate-group-detail.component.ts
@Component({
  selector: 'app-duplicate-group-detail',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    ImageComparisonComponent,
    FileMetadataTableComponent
  ],
  template: `
    <div class="group-detail">
      <header class="group-header">
        <h2>Duplicate Group</h2>
        <span class="file-count">{{ group().fileCount }} files</span>
        <span class="savings">Save {{ group().potentialSavingsBytes | fileSize }}</span>
      </header>

      <div class="group-actions">
        <button mat-stroked-button (click)="autoSelect.emit()">
          <mat-icon>auto_fix_high</mat-icon>
          Auto-select Original
        </button>
        <button mat-stroked-button color="warn" (click)="deleteNonOriginals.emit()">
          <mat-icon>delete_sweep</mat-icon>
          Delete Duplicates
        </button>
      </div>

      <div class="files-comparison">
        @for (file of group().files; track file.id) {
          <div class="file-item" [class.is-original]="file.isOriginal">
            <div class="file-preview">
              <img [src]="'/api/files/' + file.id + '/thumbnail'" [alt]="file.fileName">
              @if (file.isOriginal) {
                <div class="original-badge">Original</div>
              }
            </div>

            <div class="file-info">
              <p class="file-path">{{ file.filePath }}</p>
              <p class="file-date">{{ file.dateTaken | date:'medium' }}</p>
              <p class="file-size">{{ file.fileSizeBytes | fileSize }}</p>
            </div>

            <div class="file-actions">
              @if (!file.isOriginal) {
                <button mat-button (click)="setOriginal.emit(file.id)">
                  Set as Original
                </button>
              }
              <button mat-icon-button (click)="preview(file)">
                <mat-icon>fullscreen</mat-icon>
              </button>
            </div>
          </div>
        }
      </div>

      <app-file-metadata-table [files]="group().files">
      </app-file-metadata-table>
    </div>
  `
})
export class DuplicateGroupDetailComponent {
  group = input.required<DuplicateGroupDto>();
  setOriginal = output<string>();
  autoSelect = output<void>();
  deleteNonOriginals = output<void>();

  constructor(private dialog: MatDialog) {}

  preview(file: IndexedFileDto): void {
    this.dialog.open(ImagePreviewDialogComponent, {
      data: { file },
      maxWidth: '95vw',
      maxHeight: '95vh'
    });
  }
}
```

## Image Comparison Component

```typescript
// image-comparison.component.ts
@Component({
  selector: 'app-image-comparison',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="comparison-container">
      <div class="image-panel" #panel1>
        <img [src]="image1Url()" (wheel)="onWheel($event)">
      </div>
      <div class="divider"></div>
      <div class="image-panel" #panel2>
        <img [src]="image2Url()" (wheel)="onWheel($event)">
      </div>
    </div>
    <div class="zoom-controls">
      <button mat-icon-button (click)="zoomIn()"><mat-icon>zoom_in</mat-icon></button>
      <span>{{ zoom() }}%</span>
      <button mat-icon-button (click)="zoomOut()"><mat-icon>zoom_out</mat-icon></button>
      <button mat-icon-button (click)="resetZoom()"><mat-icon>fit_screen</mat-icon></button>
    </div>
  `
})
export class ImageComparisonComponent {
  image1Url = input.required<string>();
  image2Url = input.required<string>();
  zoom = signal(100);

  zoomIn(): void {
    this.zoom.update(z => Math.min(z + 25, 400));
  }

  zoomOut(): void {
    this.zoom.update(z => Math.max(z - 25, 25));
  }

  resetZoom(): void {
    this.zoom.set(100);
  }

  onWheel(event: WheelEvent): void {
    event.preventDefault();
    if (event.deltaY < 0) this.zoomIn();
    else this.zoomOut();
  }
}
```

## Test Coverage

- DuplicatesComponent: 80% minimum
- DuplicateGroupListComponent: 85% minimum
- DuplicateGroupDetailComponent: 85% minimum
- ImageComparisonComponent: 80% minimum
- Bulk operations: 90% minimum

## Completion Checklist

- [ ] Create DuplicatesComponent as container
- [ ] Create DuplicateGroupListComponent
- [ ] Create DuplicateGroupDetailComponent
- [ ] Create ImageComparisonComponent with synced zoom
- [ ] Create FileMetadataTableComponent for comparison
- [ ] Create BulkActionsToolbarComponent
- [ ] Implement set original functionality
- [ ] Implement auto-select with rules
- [ ] Implement delete with confirmation
- [ ] Implement bulk operations
- [ ] Add image preview dialog
- [ ] Write unit tests for all components
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
