# 002: Dashboard Component

**Priority**: P2 (User Interface)
**Agent**: A4
**Branch**: `feature/web-dashboard`
**Estimated Complexity**: Medium

## Objective

Implement the main dashboard with statistics overview, recent activity, and quick actions.

## Dependencies

- `05-web-ui/001-api-services.md`

## Acceptance Criteria

- [ ] Display total file count and storage size
- [ ] Display duplicate count and potential savings
- [ ] Show last indexing timestamp
- [ ] Display scan directory status cards
- [ ] Quick action buttons (trigger scan, view duplicates)
- [ ] Auto-refresh statistics periodically
- [ ] Responsive layout for mobile/tablet
- [ ] Loading skeleton while fetching data
- [ ] Error state handling

## TDD Steps

### Red Phase - Component Tests
```typescript
// src/app/features/dashboard/dashboard.component.spec.ts
describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let mockFileService: jasmine.SpyObj<IndexedFileService>;
  let mockDirectoryService: jasmine.SpyObj<ScanDirectoryService>;

  beforeEach(async () => {
    mockFileService = jasmine.createSpyObj('IndexedFileService', ['getStatistics']);
    mockDirectoryService = jasmine.createSpyObj('ScanDirectoryService', ['getAll']);

    mockFileService.getStatistics.and.returnValue(of({
      totalFiles: 15420,
      totalSizeBytes: 52428800000,
      duplicateGroups: 342,
      duplicateFiles: 1024,
      potentialSavingsBytes: 2147483648
    }));

    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        { provide: IndexedFileService, useValue: mockFileService },
        { provide: ScanDirectoryService, useValue: mockDirectoryService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should display statistics', () => {
    const compiled = fixture.nativeElement;
    expect(compiled.querySelector('.total-files').textContent).toContain('15,420');
    expect(compiled.querySelector('.storage-size').textContent).toContain('48.8 GB');
  });

  it('should show potential savings', () => {
    const compiled = fixture.nativeElement;
    expect(compiled.querySelector('.potential-savings').textContent).toContain('2 GB');
  });
});
```

### Green Phase
Implement component.

### Refactor Phase
Add animations, optimize refresh.

## Files to Create/Modify

```
src/Web/src/app/features/dashboard/
├── dashboard.component.ts
├── dashboard.component.html
├── dashboard.component.scss
├── dashboard.component.spec.ts
├── components/
│   ├── stats-card/
│   │   ├── stats-card.component.ts
│   │   ├── stats-card.component.html
│   │   └── stats-card.component.scss
│   ├── directory-card/
│   │   ├── directory-card.component.ts
│   │   ├── directory-card.component.html
│   │   └── directory-card.component.scss
│   └── quick-actions/
│       ├── quick-actions.component.ts
│       └── quick-actions.component.html
└── pipes/
    ├── file-size.pipe.ts
    └── file-size.pipe.spec.ts
```

## Component Implementation

```typescript
// dashboard.component.ts
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule,
    StatsCardComponent,
    DirectoryCardComponent,
    QuickActionsComponent,
    FileSizePipe
  ],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit, OnDestroy {
  statistics$ = new BehaviorSubject<FileStatistics | null>(null);
  directories$ = new BehaviorSubject<ScanDirectoryDto[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  private destroy$ = new Subject<void>();
  private readonly refreshInterval = 30000; // 30 seconds

  constructor(
    private fileService: IndexedFileService,
    private directoryService: ScanDirectoryService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadData();
    this.setupAutoRefresh();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadData(): void {
    this.loading.set(true);
    this.error.set(null);

    forkJoin({
      stats: this.fileService.getStatistics(),
      directories: this.directoryService.getAll()
    }).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: ({ stats, directories }) => {
        this.statistics$.next(stats);
        this.directories$.next(directories.items);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set('Failed to load dashboard data');
        this.loading.set(false);
      }
    });
  }

  private setupAutoRefresh(): void {
    interval(this.refreshInterval).pipe(
      takeUntil(this.destroy$)
    ).subscribe(() => this.loadData());
  }

  onViewDuplicates(): void {
    this.router.navigate(['/duplicates']);
  }

  onTriggerScan(directoryId: string): void {
    this.directoryService.triggerScan(directoryId).subscribe();
  }

  refresh(): void {
    this.loadData();
  }
}
```

## Template

```html
<!-- dashboard.component.html -->
<div class="dashboard-container">
  <header class="dashboard-header">
    <h1>Photo Index Dashboard</h1>
    <button mat-icon-button (click)="refresh()" [disabled]="loading()">
      <mat-icon>refresh</mat-icon>
    </button>
  </header>

  @if (loading()) {
    <div class="loading-skeleton">
      <mat-spinner diameter="40"></mat-spinner>
    </div>
  } @else if (error()) {
    <div class="error-state">
      <mat-icon>error</mat-icon>
      <p>{{ error() }}</p>
      <button mat-button (click)="refresh()">Retry</button>
    </div>
  } @else {
    <div class="stats-grid">
      <app-stats-card
        title="Total Files"
        [value]="(statistics$ | async)?.totalFiles | number"
        icon="photo_library">
      </app-stats-card>

      <app-stats-card
        title="Storage Used"
        [value]="(statistics$ | async)?.totalSizeBytes | fileSize"
        icon="storage">
      </app-stats-card>

      <app-stats-card
        title="Duplicates Found"
        [value]="(statistics$ | async)?.duplicateFiles | number"
        icon="content_copy"
        [highlight]="true">
      </app-stats-card>

      <app-stats-card
        title="Potential Savings"
        [value]="(statistics$ | async)?.potentialSavingsBytes | fileSize"
        icon="savings"
        [highlight]="true">
      </app-stats-card>
    </div>

    <section class="directories-section">
      <h2>Scan Directories</h2>
      <div class="directories-grid">
        @for (dir of directories$ | async; track dir.id) {
          <app-directory-card
            [directory]="dir"
            (triggerScan)="onTriggerScan($event)">
          </app-directory-card>
        }
      </div>
    </section>

    <app-quick-actions
      (viewDuplicates)="onViewDuplicates()">
    </app-quick-actions>
  }
</div>
```

## File Size Pipe

```typescript
// pipes/file-size.pipe.ts
@Pipe({ name: 'fileSize', standalone: true })
export class FileSizePipe implements PipeTransform {
  private readonly units = ['B', 'KB', 'MB', 'GB', 'TB'];

  transform(bytes: number | null | undefined): string {
    if (bytes === null || bytes === undefined) return '0 B';
    if (bytes === 0) return '0 B';

    const k = 1024;
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    const size = bytes / Math.pow(k, i);

    return `${size.toFixed(1)} ${this.units[i]}`;
  }
}
```

## Test Coverage

- DashboardComponent: 80% minimum
- StatsCardComponent: 90% minimum
- FileSizePipe: 100%
- Error handling: 100%

## Completion Checklist

- [ ] Create DashboardComponent with statistics display
- [ ] Create StatsCardComponent for reusable stat display
- [ ] Create DirectoryCardComponent for directory status
- [ ] Create QuickActionsComponent for common actions
- [ ] Create FileSizePipe for human-readable sizes
- [ ] Implement auto-refresh functionality
- [ ] Add loading skeleton states
- [ ] Add error state handling
- [ ] Implement responsive layout (mobile-first)
- [ ] Write unit tests for all components
- [ ] Write unit tests for pipe
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
