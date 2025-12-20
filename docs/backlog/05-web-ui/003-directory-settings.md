# 003: Directory Settings Component

**Priority**: P2 (User Interface)
**Agent**: A4
**Branch**: `feature/web-directory-settings`
**Estimated Complexity**: Medium

## Objective

Implement the directory management page for adding, editing, and removing scan directories.

## Dependencies

- `05-web-ui/001-api-services.md`

## Acceptance Criteria

- [ ] List all configured scan directories
- [ ] Add new directory with path validation
- [ ] Edit existing directory settings
- [ ] Delete directory with confirmation
- [ ] Toggle enable/disable for directories
- [ ] Show directory statistics (file count, size, last scan)
- [ ] Trigger manual scan per directory
- [ ] Form validation for path input
- [ ] Responsive table/card layout

## TDD Steps

### Red Phase
```typescript
describe('DirectorySettingsComponent', () => {
  it('should list all directories', () => {});
  it('should open add dialog on button click', () => {});
  it('should validate path before saving', () => {});
  it('should confirm before deleting', () => {});
  it('should toggle directory enabled state', () => {});
});

describe('DirectoryFormDialogComponent', () => {
  it('should show error for invalid path', () => {});
  it('should disable save for empty path', () => {});
  it('should emit directory on save', () => {});
});
```

### Green Phase
Implement components.

### Refactor Phase
Add optimistic updates, improve UX.

## Files to Create

```
src/Web/src/app/features/settings/
├── settings.component.ts
├── settings.component.html
├── settings.component.scss
├── settings.component.spec.ts
├── components/
│   ├── directory-list/
│   │   ├── directory-list.component.ts
│   │   ├── directory-list.component.html
│   │   └── directory-list.component.spec.ts
│   ├── directory-form-dialog/
│   │   ├── directory-form-dialog.component.ts
│   │   ├── directory-form-dialog.component.html
│   │   └── directory-form-dialog.component.spec.ts
│   └── confirm-dialog/
│       ├── confirm-dialog.component.ts
│       └── confirm-dialog.component.html
└── settings.routes.ts
```

## Component Implementation

```typescript
// settings.component.ts
@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    MatDialogModule,
    MatTooltipModule,
    DirectoryListComponent
  ],
  templateUrl: './settings.component.html'
})
export class SettingsComponent implements OnInit {
  directories = signal<ScanDirectoryDto[]>([]);
  loading = signal(true);

  constructor(
    private directoryService: ScanDirectoryService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadDirectories();
  }

  private loadDirectories(): void {
    this.loading.set(true);
    this.directoryService.getAll().subscribe({
      next: (response) => {
        this.directories.set(response.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  openAddDialog(): void {
    const dialogRef = this.dialog.open(DirectoryFormDialogComponent, {
      width: '500px',
      data: { mode: 'create' }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.directoryService.create(result).subscribe({
          next: (created) => {
            this.directories.update(dirs => [...dirs, created]);
            this.snackBar.open('Directory added', 'OK', { duration: 3000 });
          }
        });
      }
    });
  }

  openEditDialog(directory: ScanDirectoryDto): void {
    const dialogRef = this.dialog.open(DirectoryFormDialogComponent, {
      width: '500px',
      data: { mode: 'edit', directory }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.directoryService.update(directory.id, result).subscribe({
          next: (updated) => {
            this.directories.update(dirs =>
              dirs.map(d => d.id === updated.id ? updated : d)
            );
            this.snackBar.open('Directory updated', 'OK', { duration: 3000 });
          }
        });
      }
    });
  }

  confirmDelete(directory: ScanDirectoryDto): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete Directory',
        message: `Remove "${directory.path}" from scan directories?`,
        confirmText: 'Delete',
        confirmColor: 'warn'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.directoryService.delete(directory.id).subscribe({
          next: () => {
            this.directories.update(dirs => dirs.filter(d => d.id !== directory.id));
            this.snackBar.open('Directory removed', 'OK', { duration: 3000 });
          }
        });
      }
    });
  }

  toggleEnabled(directory: ScanDirectoryDto): void {
    const updated = { isEnabled: !directory.isEnabled };
    this.directoryService.update(directory.id, updated).subscribe({
      next: (result) => {
        this.directories.update(dirs =>
          dirs.map(d => d.id === result.id ? result : d)
        );
      }
    });
  }

  triggerScan(directory: ScanDirectoryDto): void {
    this.directoryService.triggerScan(directory.id).subscribe({
      next: () => {
        this.snackBar.open(`Scan started for ${directory.path}`, 'OK', { duration: 3000 });
      }
    });
  }
}
```

## Form Dialog

```typescript
// directory-form-dialog.component.ts
@Component({
  selector: 'app-directory-form-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatButtonModule
  ],
  template: `
    <h2 mat-dialog-title>{{ data.mode === 'create' ? 'Add' : 'Edit' }} Directory</h2>
    <mat-dialog-content>
      <form [formGroup]="form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Path</mat-label>
          <input matInput formControlName="path" placeholder="/photos/family">
          @if (form.controls.path.hasError('required')) {
            <mat-error>Path is required</mat-error>
          }
          @if (form.controls.path.hasError('pattern')) {
            <mat-error>Must be an absolute path</mat-error>
          }
        </mat-form-field>

        <mat-checkbox formControlName="includeSubdirectories">
          Include subdirectories
        </mat-checkbox>

        <mat-checkbox formControlName="isEnabled">
          Enabled
        </mat-checkbox>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary"
              [disabled]="form.invalid"
              (click)="save()">
        {{ data.mode === 'create' ? 'Add' : 'Save' }}
      </button>
    </mat-dialog-actions>
  `
})
export class DirectoryFormDialogComponent {
  form: FormGroup;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<DirectoryFormDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { mode: 'create' | 'edit'; directory?: ScanDirectoryDto }
  ) {
    this.form = this.fb.group({
      path: [data.directory?.path ?? '', [Validators.required, Validators.pattern(/^\/.*$/)]],
      includeSubdirectories: [data.directory?.includeSubdirectories ?? true],
      isEnabled: [data.directory?.isEnabled ?? true]
    });
  }

  save(): void {
    if (this.form.valid) {
      this.dialogRef.close(this.form.value);
    }
  }
}
```

## Test Coverage

- SettingsComponent: 80% minimum
- DirectoryFormDialogComponent: 90% minimum
- ConfirmDialogComponent: 80% minimum
- Form validation: 100%

## Completion Checklist

- [ ] Create SettingsComponent as container
- [ ] Create DirectoryListComponent for table/cards
- [ ] Create DirectoryFormDialogComponent for add/edit
- [ ] Create ConfirmDialogComponent for delete confirmation
- [ ] Implement form validation (required, path pattern)
- [ ] Add optimistic UI updates
- [ ] Implement toggle enable/disable
- [ ] Add trigger scan functionality
- [ ] Add loading and error states
- [ ] Write unit tests for all components
- [ ] Test form validation scenarios
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
