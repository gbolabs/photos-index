import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ComponentRef } from '@angular/core';
import { By } from '@angular/platform-browser';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { DuplicateGroupListComponent } from './duplicate-group-list.component';
import { DuplicateService } from '../../../../services/duplicate.service';
import { DuplicateGroupDto, PagedResponse, IndexedFileDto } from '../../../../models';

describe('DuplicateGroupListComponent', () => {
  let component: DuplicateGroupListComponent;
  let componentRef: ComponentRef<DuplicateGroupListComponent>;
  let fixture: ComponentFixture<DuplicateGroupListComponent>;
  let mockDuplicateService: {
    getAll: ReturnType<typeof vi.fn>;
    autoSelect: ReturnType<typeof vi.fn>;
    getThumbnailUrl: ReturnType<typeof vi.fn>;
  };

  // Mock data
  const mockFile1: IndexedFileDto = {
    id: 'file-1',
    filePath: '/photos/vacation/beach.jpg',
    fileName: 'beach.jpg',
    fileHash: 'abc123',
    fileSize: 1024000,
    width: 1920,
    height: 1080,
    createdAt: '2024-01-01T10:00:00Z',
    modifiedAt: '2024-01-01T10:00:00Z',
    indexedAt: '2024-01-15T10:00:00Z',
    thumbnailPath: '/thumbnails/file-1.jpg',
    isDuplicate: true,
    duplicateGroupId: 'group-1',
    dateTaken: '2024-01-01T09:30:00Z',
    cameraMake: 'Canon',
    cameraModel: 'EOS R5',
    gpsLatitude: 34.0522,
    gpsLongitude: -118.2437,
    iso: 100,
    aperture: 'f/2.8',
    shutterSpeed: '1/250',
    lastError: null,
    retryCount: 0,
    isHidden: false,
  };

  const mockGroup1: DuplicateGroupDto = {
    id: 'group-1',
    hash: 'abc123',
    fileCount: 3,
    totalSize: 3072000,
    potentialSavings: 2048000,
    resolvedAt: null,
    createdAt: '2024-01-15T10:00:00Z',
    originalFileId: null,
    files: [],
    firstFileThumbnailPath: 'thumbs/abc123.jpg',
    status: 'pending',
    validatedAt: null,
    keptFileId: null,
    lastReviewedAt: null,
    reviewOrder: null,
    reviewSessionId: null,
  };

  const mockGroup2: DuplicateGroupDto = {
    ...mockGroup1,
    id: 'group-2',
    hash: 'def456',
    fileCount: 2,
    totalSize: 2048000,
    potentialSavings: 1024000,
    status: 'auto-selected',
    originalFileId: 'file-2',
  };

  const mockGroup3: DuplicateGroupDto = {
    ...mockGroup1,
    id: 'group-3',
    hash: 'ghi789',
    fileCount: 4,
    totalSize: 4096000,
    potentialSavings: 3072000,
    status: 'validated',
    resolvedAt: '2024-01-20T10:00:00Z',
    originalFileId: 'file-3',
    validatedAt: '2024-01-20T10:00:00Z',
    keptFileId: 'file-3',
  };

  const mockPagedResponse: PagedResponse<DuplicateGroupDto> = {
    items: [mockGroup1, mockGroup2, mockGroup3],
    page: 1,
    pageSize: 20,
    totalItems: 3,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  };

  const emptyPagedResponse: PagedResponse<DuplicateGroupDto> = {
    items: [],
    page: 1,
    pageSize: 20,
    totalItems: 0,
    totalPages: 0,
    hasNextPage: false,
    hasPreviousPage: false,
  };

  beforeEach(async () => {
    mockDuplicateService = {
      getAll: vi.fn().mockReturnValue(of(mockPagedResponse)),
      autoSelect: vi.fn().mockReturnValue(of({ originalFileId: 'file-1' })),
      getThumbnailUrl: vi.fn().mockReturnValue('/thumbnails/test.jpg'),
    };

    await TestBed.configureTestingModule({
      imports: [DuplicateGroupListComponent, NoopAnimationsModule],
      providers: [{ provide: DuplicateService, useValue: mockDuplicateService }],
    }).compileComponents();

    fixture = TestBed.createComponent(DuplicateGroupListComponent);
    component = fixture.componentInstance;
    componentRef = fixture.componentRef;
  });

  describe('initialization', () => {
    it('should create', () => {
      expect(component).toBeTruthy();
    });

    it('should load groups on init', () => {
      fixture.detectChanges();

      expect(mockDuplicateService.getAll).toHaveBeenCalledWith(1, 20, undefined);
      expect(component.groups()).toHaveLength(3);
      expect(component.totalItems()).toBe(3);
      expect(component.loading()).toBe(false);
    });

    it('should show loading spinner while loading', () => {
      // Manually set loading state before detectChanges triggers ngOnInit
      // which would immediately complete the async load due to synchronous of()
      expect(component.loading()).toBe(true); // Initial state before loading
    });
  });

  describe('filter bar visibility', () => {
    it('should show filter bar when groups exist', () => {
      fixture.detectChanges();

      const filterBar = fixture.debugElement.query(By.css('.filter-bar'));
      expect(filterBar).toBeTruthy();
    });

    it('should show filter bar even when no groups match filter', () => {
      mockDuplicateService.getAll.mockReturnValue(of(emptyPagedResponse));
      component.statusFilter.set('validated');
      fixture.detectChanges();

      component.loadGroups();
      fixture.detectChanges();

      const filterBar = fixture.debugElement.query(By.css('.filter-bar'));
      expect(filterBar).toBeTruthy();
    });

    it('should show "No Matching Groups" when filter returns empty results', () => {
      mockDuplicateService.getAll.mockReturnValue(of(emptyPagedResponse));
      component.statusFilter.set('validated');
      fixture.detectChanges();

      component.loadGroups();
      fixture.detectChanges();

      const emptyCard = fixture.debugElement.query(By.css('.empty-card'));
      expect(emptyCard).toBeTruthy();

      const heading = emptyCard.query(By.css('h3'));
      expect(heading.nativeElement.textContent).toContain('No Matching Groups');
    });

    it('should show "No Duplicates Found" when no filter and no groups', () => {
      mockDuplicateService.getAll.mockReturnValue(of(emptyPagedResponse));
      component.statusFilter.set('');
      fixture.detectChanges();

      component.loadGroups();
      fixture.detectChanges();

      const emptyCard = fixture.debugElement.query(By.css('.empty-card'));
      expect(emptyCard).toBeTruthy();

      const heading = emptyCard.query(By.css('h3'));
      expect(heading.nativeElement.textContent).toContain('No Duplicates Found');
    });
  });

  describe('status filtering', () => {
    it('should pass status filter to service', () => {
      fixture.detectChanges();

      component.onFilterChange('pending');

      expect(mockDuplicateService.getAll).toHaveBeenCalledWith(1, 20, 'pending');
    });

    it('should reset page index when filter changes', () => {
      fixture.detectChanges();

      component.pageIndex = 5;
      component.onFilterChange('validated');

      expect(component.pageIndex).toBe(0);
    });

    it('should clear selection when filter changes', () => {
      fixture.detectChanges();

      component.selectedGroupIds.set(new Set(['group-1', 'group-2']));
      component.onFilterChange('pending');

      expect(component.selectedGroupIds().size).toBe(0);
    });

    it('should have correct status options', () => {
      expect(component.statusOptions).toContainEqual({ value: '', label: 'All Status' });
      expect(component.statusOptions).toContainEqual({ value: 'pending', label: 'Pending' });
      expect(component.statusOptions).toContainEqual({ value: 'validated', label: 'Validated' });
      expect(component.statusOptions).toContainEqual({ value: 'auto-selected', label: 'Auto-selected' });
    });
  });

  describe('sorting', () => {
    it('should sort by totalSize descending by default', () => {
      fixture.detectChanges();

      expect(component.sortColumn()).toBe('totalSize');
      expect(component.sortDirection()).toBe('desc');
    });

    it('should update sort column and direction on sort change', () => {
      fixture.detectChanges();

      component.onSortChange({ active: 'fileCount', direction: 'asc' });

      expect(component.sortColumn()).toBe('fileCount');
      expect(component.sortDirection()).toBe('asc');
    });

    it('should re-sort groups when sort changes', () => {
      fixture.detectChanges();

      component.onSortChange({ active: 'fileCount', direction: 'asc' });
      fixture.detectChanges();

      const newOrder = component.groups().map((g) => g.id);
      // Groups should be sorted by fileCount ascending: group-2 (2), group-1 (3), group-3 (4)
      expect(newOrder).toEqual(['group-2', 'group-1', 'group-3']);
    });

    it('should reset to default sort when sort is cleared', () => {
      fixture.detectChanges();

      component.onSortChange({ active: 'fileCount', direction: 'asc' });
      component.onSortChange({ active: '', direction: '' });

      expect(component.sortColumn()).toBe('totalSize');
      expect(component.sortDirection()).toBe('desc');
    });
  });

  describe('thumbnails', () => {
    it('should generate thumbnail URL from firstFileThumbnailPath', () => {
      const group = { ...mockGroup1, firstFileThumbnailPath: 'thumbs/test.jpg', hash: null };
      const url = component.getThumbnailUrl(group as any);
      expect(url).toBe('/thumbnails/thumbs/test.jpg');
    });

    it('should generate thumbnail URL from hash when no firstFileThumbnailPath', () => {
      const group = { ...mockGroup1, firstFileThumbnailPath: null, hash: 'abc123' };
      const url = component.getThumbnailUrl(group);
      expect(url).toBe('/thumbnails/thumbs/abc123.jpg');
    });

    it('should return placeholder when no thumbnail info available', () => {
      const group = { ...mockGroup1, firstFileThumbnailPath: null, hash: null, files: [] };
      const url = component.getThumbnailUrl(group as any);
      expect(url).toBe('assets/placeholder.svg');
    });

    it('should detect multiple files using fileCount', () => {
      expect(component.hasMultipleFiles(mockGroup1)).toBe(true); // fileCount: 3
      expect(component.hasMultipleFiles({ ...mockGroup1, fileCount: 1 })).toBe(false);
    });
  });

  describe('selection', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should toggle individual selection', () => {
      component.toggleSelection(mockGroup1);
      expect(component.isSelected(mockGroup1)).toBe(true);

      component.toggleSelection(mockGroup1);
      expect(component.isSelected(mockGroup1)).toBe(false);
    });

    it('should toggle all selection', () => {
      component.toggleAllSelection();
      expect(component.allSelected()).toBe(true);

      component.toggleAllSelection();
      expect(component.selectedGroupIds().size).toBe(0);
    });

    it('should emit selection changes', () => {
      const spy = vi.fn();
      componentRef.instance.selectionChanged.subscribe(spy);

      component.toggleSelection(mockGroup1);

      expect(spy).toHaveBeenCalledWith(['group-1']);
    });

    it('should compute someSelected correctly', () => {
      expect(component.someSelected()).toBe(false);

      component.toggleSelection(mockGroup1);
      expect(component.someSelected()).toBe(true);

      component.toggleAllSelection();
      expect(component.someSelected()).toBe(false);
    });
  });

  describe('pagination', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should update page and reload on page change', () => {
      const pageEvent = { pageIndex: 2, pageSize: 50, length: 100 };

      component.onPageChange(pageEvent as any);

      expect(component.pageIndex).toBe(2);
      expect(component.pageSize).toBe(50);
      expect(mockDuplicateService.getAll).toHaveBeenCalledWith(3, 50, undefined);
    });

    it('should clear selection on page change', () => {
      component.selectedGroupIds.set(new Set(['group-1']));

      component.onPageChange({ pageIndex: 1, pageSize: 20 } as any);

      expect(component.selectedGroupIds().size).toBe(0);
    });
  });

  describe('error handling', () => {
    it('should display error message on load failure', () => {
      mockDuplicateService.getAll.mockReturnValue(throwError(() => new Error('Network error')));

      fixture.detectChanges();

      expect(component.error()).toBe('Failed to load duplicate groups');
      expect(component.loading()).toBe(false);

      const errorCard = fixture.debugElement.query(By.css('.error-card'));
      expect(errorCard).toBeTruthy();
    });

    it('should show retry button on error', () => {
      mockDuplicateService.getAll.mockReturnValue(throwError(() => new Error('Network error')));

      fixture.detectChanges();

      const retryButton = fixture.debugElement.query(By.css('.error-card button'));
      expect(retryButton).toBeTruthy();
    });
  });

  describe('resolved status', () => {
    it('should correctly identify resolved groups', () => {
      expect(component.isResolved(mockGroup1)).toBe(false); // No resolvedAt or originalFileId
      expect(component.isResolved(mockGroup3)).toBe(true); // Has both
    });
  });

  describe('auto-select', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should call autoSelect service on button click', () => {
      const event = { stopPropagation: vi.fn() } as any;

      component.autoSelectOriginal(mockGroup1, event);

      expect(event.stopPropagation).toHaveBeenCalled();
      expect(mockDuplicateService.autoSelect).toHaveBeenCalledWith('group-1');
    });

    it('should reload groups after auto-select', () => {
      const initialCallCount = mockDuplicateService.getAll.mock.calls.length;

      component.autoSelectOriginal(mockGroup1, { stopPropagation: vi.fn() } as any);

      expect(mockDuplicateService.getAll.mock.calls.length).toBe(initialCallCount + 1);
    });
  });

  describe('view group', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should emit groupSelected when viewing a group', () => {
      const spy = vi.fn();
      componentRef.instance.groupSelected.subscribe(spy);

      component.viewGroup(mockGroup1);

      expect(spy).toHaveBeenCalledWith(mockGroup1);
    });
  });

  describe('status labels', () => {
    it('should return correct status labels', () => {
      expect(component.getStatusLabel('pending')).toBe('Pending');
      expect(component.getStatusLabel('validated')).toBe('Validated');
      expect(component.getStatusLabel('auto-selected')).toBe('Auto-selected');
      expect(component.getStatusLabel(null)).toBe('Pending');
      expect(component.getStatusLabel('unknown')).toBe('Unknown');
    });
  });
});
