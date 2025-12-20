import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { DirectoryListComponent } from './directory-list.component';
import { ScanDirectoryDto } from '../../../../core/models';

describe('DirectoryListComponent', () => {
  let component: DirectoryListComponent;
  let fixture: ComponentFixture<DirectoryListComponent>;

  const mockDirectories: ScanDirectoryDto[] = [
    {
      id: '1',
      path: '/test/path1',
      isEnabled: true,
      lastScannedAt: '2024-01-15T10:00:00Z',
      createdAt: '2024-01-01T00:00:00Z',
      fileCount: 50,
    },
    {
      id: '2',
      path: '/test/path2',
      isEnabled: false,
      lastScannedAt: null,
      createdAt: '2024-01-02T00:00:00Z',
      fileCount: 0,
    },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DirectoryListComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();

    fixture = TestBed.createComponent(DirectoryListComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('directories', mockDirectories);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display directories in table', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('tr.mat-mdc-row');
    expect(rows.length).toBe(2);
  });

  it('should display directory paths', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('/test/path1');
    expect(compiled.textContent).toContain('/test/path2');
  });

  it('should display file counts', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('50');
    expect(compiled.textContent).toContain('0');
  });

  it('should emit edit event when edit button clicked', () => {
    let emittedDirectory: ScanDirectoryDto | undefined;
    component.edit.subscribe((dir) => (emittedDirectory = dir));

    component.onEdit(mockDirectories[0]);
    expect(emittedDirectory).toEqual(mockDirectories[0]);
  });

  it('should emit delete event when delete button clicked', () => {
    let emittedDirectory: ScanDirectoryDto | undefined;
    component.delete.subscribe((dir) => (emittedDirectory = dir));

    component.onDelete(mockDirectories[0]);
    expect(emittedDirectory).toEqual(mockDirectories[0]);
  });

  it('should emit toggle event when toggle button clicked', () => {
    let emittedDirectory: ScanDirectoryDto | undefined;
    component.toggle.subscribe((dir) => (emittedDirectory = dir));

    component.onToggle(mockDirectories[0]);
    expect(emittedDirectory).toEqual(mockDirectories[0]);
  });

  it('should format date correctly', () => {
    expect(component.formatDate('2024-01-15T10:00:00Z')).toContain('2024');
  });

  it('should handle null date', () => {
    expect(component.formatDate(null)).toBe('Never');
  });

  it('should show empty state when no directories', () => {
    fixture.componentRef.setInput('directories', []);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.empty-state')).toBeTruthy();
    expect(compiled.textContent).toContain('No directories configured');
  });
});
