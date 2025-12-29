import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MatDialog } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';
import { BulkActionsToolbarComponent } from './bulk-actions-toolbar.component';
import { DuplicateService } from '../../../../services/duplicate.service';
import { FileStatisticsDto } from '../../../../models';

describe('BulkActionsToolbarComponent', () => {
  let component: BulkActionsToolbarComponent;
  let fixture: ComponentFixture<BulkActionsToolbarComponent>;
  let duplicateService: DuplicateService;

  const mockStats: FileStatisticsDto = {
    totalFiles: 1000,
    totalSizeBytes: 5000000000,
    duplicateGroups: 50,
    duplicateFiles: 150,
    potentialSavingsBytes: 500000000,
    lastIndexedAt: '2024-01-15T10:00:00Z',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BulkActionsToolbarComponent],
      providers: [
        DuplicateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        { provide: MatDialog, useValue: { open: vi.fn() } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(BulkActionsToolbarComponent);
    component = fixture.componentInstance;
    duplicateService = TestBed.inject(DuplicateService);
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load statistics on init', () => {
    vi.spyOn(duplicateService, 'getStatistics').mockReturnValue(of(mockStats));

    component.ngOnInit();

    expect(duplicateService.getStatistics).toHaveBeenCalled();
    expect(component.stats()).toEqual(mockStats);
  });

  it('should handle error when loading statistics fails', () => {
    const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(duplicateService, 'getStatistics').mockReturnValue(
      throwError(() => new Error('API Error'))
    );

    component.ngOnInit();

    expect(consoleErrorSpy).toHaveBeenCalled();
    consoleErrorSpy.mockRestore();
  });

  it('should compute selection count correctly', () => {
    fixture.componentRef.setInput('selectedGroupIds', ['id1', 'id2', 'id3']);
    expect(component.selectionCount).toBe(3);
  });

  it('should compute hasSelection correctly', () => {
    fixture.componentRef.setInput('selectedGroupIds', []);
    expect(component.hasSelection).toBe(false);

    fixture.componentRef.setInput('selectedGroupIds', ['id1']);
    expect(component.hasSelection).toBe(true);
  });

  it('should auto-select all with confirmation', () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const autoSelectResult = { groupsProcessed: 25 };
    vi.spyOn(duplicateService, 'autoSelectAll').mockReturnValue(of(autoSelectResult));
    vi.spyOn(component, 'loadStats');

    component.autoSelectAll();

    expect(confirmSpy).toHaveBeenCalled();
    expect(duplicateService.autoSelectAll).toHaveBeenCalled();
    expect(component.loading()).toBe(false);
    expect(component.loadStats).toHaveBeenCalled();

    confirmSpy.mockRestore();
  });

  it('should not auto-select all without confirmation', () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    vi.spyOn(duplicateService, 'autoSelectAll');

    component.autoSelectAll();

    expect(confirmSpy).toHaveBeenCalled();
    expect(duplicateService.autoSelectAll).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
  });

  it('should handle error during auto-select all', () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(duplicateService, 'autoSelectAll').mockReturnValue(
      throwError(() => new Error('API Error'))
    );

    component.autoSelectAll();

    expect(component.loading()).toBe(false);
    expect(consoleErrorSpy).toHaveBeenCalled();

    confirmSpy.mockRestore();
    consoleErrorSpy.mockRestore();
  });

  it('should emit actionCompleted after successful auto-select', () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const autoSelectResult = { groupsProcessed: 25 };
    vi.spyOn(duplicateService, 'autoSelectAll').mockReturnValue(of(autoSelectResult));

    let actionCompletedEmitted = false;
    component.actionCompleted.subscribe(() => {
      actionCompletedEmitted = true;
    });

    component.autoSelectAll();

    expect(actionCompletedEmitted).toBe(true);

    confirmSpy.mockRestore();
  });

  it('should refresh statistics and emit refreshRequested', () => {
    vi.spyOn(duplicateService, 'getStatistics').mockReturnValue(of(mockStats));

    let refreshRequestedEmitted = false;
    component.refreshRequested.subscribe(() => {
      refreshRequestedEmitted = true;
    });

    component.refresh();

    expect(duplicateService.getStatistics).toHaveBeenCalled();
    expect(refreshRequestedEmitted).toBe(true);
  });

  it('should display statistics in template when loaded', () => {
    vi.spyOn(duplicateService, 'getStatistics').mockReturnValue(of(mockStats));
    component.ngOnInit();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const toolbar = compiled.querySelector('mat-toolbar');
    expect(toolbar).toBeTruthy();
  });

});
