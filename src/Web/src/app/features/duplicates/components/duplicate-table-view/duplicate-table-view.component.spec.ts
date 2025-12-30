import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ComponentRef, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatCardModule } from '@angular/material/card';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { DuplicateTableViewComponent } from './duplicate-table-view.component';
import { DuplicateService } from '../../../../services/duplicate.service';
import { DuplicateGroupDto, PagedResponse, IndexedFileDto } from '../../../../models';
import { FileSizePipe } from '../../../../shared/pipes/file-size.pipe';

describe('DuplicateTableViewComponent', () => {
  let component: DuplicateTableViewComponent;
  let componentRef: ComponentRef<DuplicateTableViewComponent>;
  let fixture: ComponentFixture<DuplicateTableViewComponent>;
  let mockDuplicateService: { getAll: ReturnType<typeof vi.fn>; getById: ReturnType<typeof vi.fn> };

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
    isDeleted: false,
  };

  const mockFile2: IndexedFileDto = {
    ...mockFile1,
    id: 'file-2',
    filePath: '/photos/backup/beach.jpg',
    fileName: 'beach.jpg',
    fileSize: 1024000,
    modifiedAt: '2024-01-02T10:00:00Z',
  };

  const mockFile3: IndexedFileDto = {
    ...mockFile1,
    id: 'file-3',
    filePath: '/photos/archive/beach_copy.jpg',
    fileName: 'beach_copy.jpg',
    fileSize: 1024000,
    modifiedAt: '2024-01-03T10:00:00Z',
  };

  const mockGroup1: DuplicateGroupDto = {
    id: 'group-1',
    hash: 'abc123',
    fileCount: 3,
    totalSize: 3072000,
    potentialSavings: 2048000,
    resolvedAt: null,
    createdAt: '2024-01-15T10:00:00Z',
    originalFileId: 'file-1',
    files: [mockFile1, mockFile2, mockFile3],
    firstFileThumbnailPath: null,
    status: 'pending',
    validatedAt: null,
    keptFileId: null,
    lastReviewedAt: null,
    reviewOrder: null,
    reviewSessionId: null,
  };

  const mockGroup2: DuplicateGroupDto = {
    id: 'group-2',
    hash: 'def456',
    fileCount: 2,
    totalSize: 2048000,
    potentialSavings: 1024000,
    resolvedAt: null,
    createdAt: '2024-01-16T10:00:00Z',
    originalFileId: null, // No original selected
    files: [
      { ...mockFile1, id: 'file-4', filePath: '/photos/sunset.jpg', fileSize: 1024000 },
      { ...mockFile1, id: 'file-5', filePath: '/photos/sunset_2.jpg', fileSize: 1024000 },
    ],
    firstFileThumbnailPath: null,
    status: 'pending',
    validatedAt: null,
    keptFileId: null,
    lastReviewedAt: null,
    reviewOrder: null,
    reviewSessionId: null,
  };

  const mockGroup3: DuplicateGroupDto = {
    id: 'group-3',
    hash: 'ghi789',
    fileCount: 2,
    totalSize: 4096000,
    potentialSavings: 2048000,
    resolvedAt: '2024-01-20T10:00:00Z', // Validated
    createdAt: '2024-01-17T10:00:00Z',
    originalFileId: 'file-6',
    files: [
      { ...mockFile1, id: 'file-6', filePath: '/photos/mountain.jpg', fileSize: 2048000 },
      { ...mockFile1, id: 'file-7', filePath: '/photos/mountain_copy.jpg', fileSize: 2048000 },
    ],
    firstFileThumbnailPath: null,
    status: 'validated',
    validatedAt: '2024-01-20T10:00:00Z',
    keptFileId: 'file-6',
    lastReviewedAt: null,
    reviewOrder: null,
    reviewSessionId: null,
  };

  const mockPagedResponse: PagedResponse<DuplicateGroupDto> = {
    items: [mockGroup1, mockGroup2, mockGroup3],
    page: 1,
    pageSize: 50,
    totalItems: 3,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  };

  beforeEach(async () => {
    mockDuplicateService = {
      getAll: vi.fn().mockReturnValue(of(mockPagedResponse)),
      getById: vi.fn().mockImplementation((id: string) => {
        const groups: Record<string, DuplicateGroupDto> = {
          'group-1': mockGroup1,
          'group-2': mockGroup2,
          'group-3': mockGroup3,
        };
        return of(groups[id] || mockGroup1);
      }),
    };

    await TestBed.configureTestingModule({
      imports: [
        DuplicateTableViewComponent,
        NoopAnimationsModule,
        MatTableModule,
        MatButtonModule,
        MatIconModule,
        MatPaginatorModule,
        MatProgressSpinnerModule,
        MatCheckboxModule,
        MatTooltipModule,
        MatSortModule,
        MatCardModule,
        FileSizePipe,
      ],
      providers: [{ provide: DuplicateService, useValue: mockDuplicateService }],
    }).compileComponents();

    fixture = TestBed.createComponent(DuplicateTableViewComponent);
    component = fixture.componentInstance;
    componentRef = fixture.componentRef;
  });

  describe('Component Initialization', () => {
    it('should create the component', () => {
      expect(component).toBeTruthy();
    });

    it('should call loadGroups on ngOnInit', () => {
      const loadGroupsSpy = vi.spyOn(component, 'loadGroups');

      component.ngOnInit();

      expect(loadGroupsSpy).toHaveBeenCalled();
    });

    it('should initialize with default pagination values', () => {
      expect(component.pageIndex).toBe(0);
      expect(component.pageSize).toBe(50);
      expect(component.pageSizeOptions).toEqual([50, 100, 500]);
    });

    it('should initialize with default sort settings', () => {
      expect(component.sortColumn()).toBe('size');
      expect(component.sortDirection()).toBe('desc');
    });

    it('should initialize with correct displayedColumns', () => {
      expect(component.displayedColumns).toEqual(['select', 'thumbnail', 'original', 'size', 'date', 'status', 'duplicates']);
    });

    it('should initialize with loading state true', () => {
      expect(component.loading()).toBe(true);
    });

    it('should initialize with empty groups', () => {
      expect(component.groups().length).toBe(0);
    });
  });

  describe('Data Loading', () => {
    it('should load groups successfully', () => {
      fixture.detectChanges();

      expect(mockDuplicateService.getAll).toHaveBeenCalledWith(1, 50, undefined);
      expect(component.groups().length).toBe(3);
      expect(component.totalItems()).toBe(3);
      expect(component.loading()).toBe(false);
      expect(component.error()).toBeNull();
    });

    it('should set loading to true initially and false after completion', () => {
      // With synchronous observables (of()), loading goes from true to false immediately
      // So we check the final state after loadGroups completes
      expect(component.loading()).toBe(true); // Initial state before any loading

      fixture.detectChanges(); // Triggers ngOnInit which calls loadGroups

      expect(component.loading()).toBe(false); // Loading completed
    });

    it('should handle loading errors', () => {
      const error = new Error('Failed to load');
      mockDuplicateService.getAll.mockReturnValue(throwError(() => error));

      fixture.detectChanges();

      expect(component.error()).toBe('Failed to load duplicate groups');
      expect(component.loading()).toBe(false);
    });

    it('should call service with correct page parameters', () => {
      component.pageIndex = 2;
      component.pageSize = 100;

      component.loadGroups();

      expect(mockDuplicateService.getAll).toHaveBeenCalledWith(3, 100, undefined); // pageIndex + 1
    });

    it('should sort groups after loading', () => {
      component.sortColumn.set('fileCount');
      component.sortDirection.set('asc');

      fixture.detectChanges();

      const groups = component.groups();
      expect(groups[0].fileCount).toBeLessThanOrEqual(groups[1].fileCount);
    });
  });

  describe('Pagination', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should handle page change', () => {
      const pageEvent: PageEvent = {
        pageIndex: 1,
        pageSize: 50,
        length: 100,
      };

      const loadGroupsSpy = vi.spyOn(component, 'loadGroups');

      component.onPageChange(pageEvent);

      expect(component.pageIndex).toBe(1);
      expect(component.pageSize).toBe(50);
      expect(loadGroupsSpy).toHaveBeenCalled();
    });

    it('should clear selection on page change', () => {
      component.selectedGroupIds.set(new Set(['group-1', 'group-2']));

      const pageEvent: PageEvent = {
        pageIndex: 1,
        pageSize: 50,
        length: 100,
      };

      component.onPageChange(pageEvent);

      expect(component.selectedGroupIds().size).toBe(0);
    });

    it('should update pageSize when changed', () => {
      const pageEvent: PageEvent = {
        pageIndex: 0,
        pageSize: 100,
        length: 100,
      };

      component.onPageChange(pageEvent);

      expect(component.pageSize).toBe(100);
    });
  });

  describe('Sorting', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should handle sort change by size descending', () => {
      const sortEvent: Sort = {
        active: 'size',
        direction: 'desc',
      };

      component.onSortChange(sortEvent);

      const groups = component.groups();
      expect(groups[0].totalSize).toBeGreaterThanOrEqual(groups[1].totalSize);
      expect(groups[1].totalSize).toBeGreaterThanOrEqual(groups[2].totalSize);
    });

    it('should handle sort change by size ascending', () => {
      const sortEvent: Sort = {
        active: 'size',
        direction: 'asc',
      };

      component.onSortChange(sortEvent);

      const groups = component.groups();
      expect(groups[0].totalSize).toBeLessThanOrEqual(groups[1].totalSize);
      expect(groups[1].totalSize).toBeLessThanOrEqual(groups[2].totalSize);
    });

    it('should handle sort change by date', () => {
      const sortEvent: Sort = {
        active: 'date',
        direction: 'desc',
      };

      component.onSortChange(sortEvent);

      expect(component.sortColumn()).toBe('date');
      expect(component.sortDirection()).toBe('desc');
    });

    it('should handle sort change by fileCount', () => {
      const sortEvent: Sort = {
        active: 'fileCount',
        direction: 'asc',
      };

      component.onSortChange(sortEvent);

      const groups = component.groups();
      expect(groups[0].fileCount).toBeLessThanOrEqual(groups[1].fileCount);
    });

    it('should ignore sort change with no active column', () => {
      const initialGroups = [...component.groups()];
      const sortEvent: Sort = {
        active: '',
        direction: 'asc',
      };

      component.onSortChange(sortEvent);

      expect(component.groups()).toEqual(initialGroups);
    });

    it('should ignore sort change with no direction', () => {
      const initialGroups = [...component.groups()];
      const sortEvent: Sort = {
        active: 'size',
        direction: '',
      };

      component.onSortChange(sortEvent);

      expect(component.groups()).toEqual(initialGroups);
    });
  });

  describe('Row Expansion', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should expand row when toggled', () => {
      expect(component.isRowExpanded('group-1')).toBe(false);

      component.toggleRowExpansion('group-1');

      expect(component.isRowExpanded('group-1')).toBe(true);
    });

    it('should collapse expanded row when toggled again', () => {
      component.toggleRowExpansion('group-1');
      expect(component.isRowExpanded('group-1')).toBe(true);

      component.toggleRowExpansion('group-1');

      expect(component.isRowExpanded('group-1')).toBe(false);
    });

    it('should maintain expansion state for multiple rows', () => {
      component.toggleRowExpansion('group-1');
      component.toggleRowExpansion('group-2');

      expect(component.isRowExpanded('group-1')).toBe(true);
      expect(component.isRowExpanded('group-2')).toBe(true);
      expect(component.isRowExpanded('group-3')).toBe(false);
    });

    it('should handle isExpandedRow trackBy function', () => {
      component.toggleRowExpansion('group-1');

      const result = component.isExpandedRow(0, mockGroup1);

      expect(result).toBe(true);
    });
  });

  describe('Selection', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should select a group', () => {
      component.toggleSelection(mockGroup1);

      expect(component.isSelected(mockGroup1)).toBe(true);
      expect(component.selectedGroupIds().has('group-1')).toBe(true);
    });

    it('should deselect a selected group', () => {
      component.toggleSelection(mockGroup1);
      expect(component.isSelected(mockGroup1)).toBe(true);

      component.toggleSelection(mockGroup1);

      expect(component.isSelected(mockGroup1)).toBe(false);
      expect(component.selectedGroupIds().has('group-1')).toBe(false);
    });

    it('should emit selectionChanged when selection is toggled', async () => {
      const promise = new Promise<string[]>((resolve) => {
        component.selectionChanged.subscribe((selectedIds: string[]) => {
          resolve(selectedIds);
        });
      });

      component.toggleSelection(mockGroup1);

      const selectedIds = await promise;
      expect(selectedIds).toContain('group-1');
      expect(selectedIds.length).toBe(1);
    });

    it('should select multiple groups', () => {
      component.toggleSelection(mockGroup1);
      component.toggleSelection(mockGroup2);

      expect(component.selectedGroupIds().size).toBe(2);
      expect(component.isSelected(mockGroup1)).toBe(true);
      expect(component.isSelected(mockGroup2)).toBe(true);
    });

    it('should calculate allSelected correctly when all are selected', () => {
      component.toggleSelection(mockGroup1);
      component.toggleSelection(mockGroup2);
      component.toggleSelection(mockGroup3);

      expect(component.allSelected()).toBe(true);
    });

    it('should calculate allSelected as false when not all are selected', () => {
      component.toggleSelection(mockGroup1);

      expect(component.allSelected()).toBe(false);
    });

    it('should calculate someSelected correctly', () => {
      component.toggleSelection(mockGroup1);

      expect(component.someSelected()).toBe(true);
      expect(component.allSelected()).toBe(false);
    });

    it('should calculate someSelected as false when none selected', () => {
      expect(component.someSelected()).toBe(false);
    });

    it('should calculate someSelected as false when all selected', () => {
      component.toggleSelection(mockGroup1);
      component.toggleSelection(mockGroup2);
      component.toggleSelection(mockGroup3);

      expect(component.someSelected()).toBe(false);
      expect(component.allSelected()).toBe(true);
    });
  });

  describe('Toggle All Selection', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should select all groups when none are selected', () => {
      component.toggleAllSelection();

      expect(component.selectedGroupIds().size).toBe(3);
      expect(component.allSelected()).toBe(true);
    });

    it('should deselect all groups when all are selected', () => {
      component.toggleAllSelection(); // Select all
      expect(component.allSelected()).toBe(true);

      component.toggleAllSelection(); // Deselect all

      expect(component.selectedGroupIds().size).toBe(0);
      expect(component.allSelected()).toBe(false);
    });

    it('should select all when some are selected', () => {
      component.toggleSelection(mockGroup1);

      component.toggleAllSelection();

      expect(component.selectedGroupIds().size).toBe(3);
      expect(component.allSelected()).toBe(true);
    });

    it('should emit selectionChanged when toggling all', async () => {
      const promise = new Promise<string[]>((resolve) => {
        component.selectionChanged.subscribe((selectedIds: string[]) => {
          resolve(selectedIds);
        });
      });

      component.toggleAllSelection();

      const selectedIds = await promise;
      expect(selectedIds.length).toBe(3);
    });
  });

  describe('Status Color Coding', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should return status-auto-selected for groups with original but not resolved', () => {
      const status = component.getRowStatusClass(mockGroup1);

      expect(status).toBe('status-auto-selected');
    });

    it('should return status-conflict for groups without original', () => {
      const status = component.getRowStatusClass(mockGroup2);

      expect(status).toBe('status-conflict');
    });

    it('should return status-validated for resolved groups', () => {
      const status = component.getRowStatusClass(mockGroup3);

      expect(status).toBe('status-validated');
    });
  });

  describe('File Helpers', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should get original file from group', () => {
      const original = component.getOriginalFile(mockGroup1);

      expect(original).toBeTruthy();
      expect(original?.id).toBe('file-1');
    });

    it('should return null when no original file is set', () => {
      const original = component.getOriginalFile(mockGroup2);

      expect(original).toBeNull();
    });

    it('should get duplicate files excluding original', () => {
      const duplicates = component.getDuplicateFiles(mockGroup1);

      expect(duplicates.length).toBe(2);
      expect(duplicates.find((f) => f.id === 'file-1')).toBeUndefined();
      expect(duplicates.find((f) => f.id === 'file-2')).toBeTruthy();
      expect(duplicates.find((f) => f.id === 'file-3')).toBeTruthy();
    });

    it('should return all files when no original is set', () => {
      const duplicates = component.getDuplicateFiles(mockGroup2);

      expect(duplicates.length).toBe(2);
    });

    it('should return empty array when group has no files', () => {
      const emptyGroup = { ...mockGroup1, files: [] };
      const duplicates = component.getDuplicateFiles(emptyGroup);

      expect(duplicates).toEqual([]);
    });

    it('should get latest date from files', () => {
      const latestDate = component.getLatestDate(mockGroup1);
      const date = new Date(latestDate);

      expect(date.toISOString()).toBe('2024-01-03T10:00:00.000Z'); // mockFile3 has latest date
    });

    it('should return createdAt when no files exist', () => {
      const emptyGroup = { ...mockGroup1, files: [] };
      const latestDate = component.getLatestDate(emptyGroup);

      expect(latestDate).toBe(mockGroup1.createdAt);
    });
  });

  describe('Path Truncation', () => {
    it('should not truncate short paths', () => {
      const path = '/photos/img.jpg';
      const result = component.getTruncatedPath(path, 40);

      expect(result).toBe(path);
    });

    it('should truncate long paths', () => {
      const path = '/very/long/path/to/some/deeply/nested/directory/structure/image.jpg';
      const result = component.getTruncatedPath(path, 40);

      expect(result.length).toBeLessThanOrEqual(40);
      expect(result).toContain('...');
      expect(result).toContain('image.jpg');
    });

    it('should preserve filename in truncated path', () => {
      const path = '/very/long/path/to/directory/vacation_beach_sunset_2024.jpg';
      const result = component.getTruncatedPath(path, 40);

      expect(result).toContain('vacation_beach_sunset_2024.jpg');
    });

    it('should handle edge case with very long filename', () => {
      const path = '/path/very_very_very_very_very_long_filename_that_exceeds_max_length.jpg';
      const result = component.getTruncatedPath(path, 40);

      // When filename itself exceeds maxLength, the function preserves the filename
      // and adds ellipsis for the path part, so result may exceed maxLength
      expect(result).toContain('...');
      // Verify truncation happened (result is different from original)
      expect(result.length).toBeLessThan(path.length);
    });

    it('should use default maxLength of 40', () => {
      const path = '/very/long/path/to/some/deeply/nested/directory/structure/image.jpg';
      const result = component.getTruncatedPath(path);

      expect(result.length).toBeLessThanOrEqual(40);
    });
  });

  describe('Date Formatting', () => {
    it('should format date correctly', () => {
      const dateString = '2024-01-15T10:30:00Z';
      const result = component.formatDate(dateString);

      expect(result).toMatch(/Jan.*15.*2024/);
    });

    it('should handle different date formats', () => {
      const dateString = '2024-12-25T00:00:00Z';
      const result = component.formatDate(dateString);

      expect(result).toMatch(/Dec.*25.*2024/);
    });
  });

  describe('Output Events', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should emit groupSelected output', async () => {
      const promise = new Promise<DuplicateGroupDto>((resolve) => {
        component.groupSelected.subscribe((group: DuplicateGroupDto) => {
          resolve(group);
        });
      });

      component.groupSelected.emit(mockGroup1);

      const group = await promise;
      expect(group).toBe(mockGroup1);
    });

    it('should emit selectionChanged with correct data', async () => {
      let receivedIds: string[] = [];

      component.selectionChanged.subscribe((ids: string[]) => {
        receivedIds = ids;
      });

      component.toggleSelection(mockGroup1);
      component.toggleSelection(mockGroup2);

      expect(receivedIds).toEqual(expect.arrayContaining(['group-1', 'group-2']));
      expect(receivedIds.length).toBe(2);
    });
  });

  describe('Edge Cases and Error Handling', () => {
    it('should handle empty response', () => {
      const emptyResponse: PagedResponse<DuplicateGroupDto> = {
        items: [],
        page: 1,
        pageSize: 50,
        totalItems: 0,
        totalPages: 0,
        hasNextPage: false,
        hasPreviousPage: false,
      };

      mockDuplicateService.getAll.mockReturnValue(of(emptyResponse));

      fixture.detectChanges();

      expect(component.groups().length).toBe(0);
      expect(component.totalItems()).toBe(0);
      expect(component.loading()).toBe(false);
    });

    it('should handle groups without files array', () => {
      const groupWithoutFiles = { ...mockGroup1, files: undefined } as any;
      const duplicates = component.getDuplicateFiles(groupWithoutFiles);

      expect(duplicates).toEqual([]);
    });

    it('should handle missing originalFileId gracefully', () => {
      const groupWithoutOriginal = { ...mockGroup1, originalFileId: null };
      const original = component.getOriginalFile(groupWithoutOriginal);

      expect(original).toBeNull();
    });

    it('should handle network errors gracefully', () => {
      mockDuplicateService.getAll.mockReturnValue(
        throwError(() => new Error('Network error'))
      );

      fixture.detectChanges();

      expect(component.error()).toBe('Failed to load duplicate groups');
      expect(component.loading()).toBe(false);
      expect(component.groups().length).toBe(0);
    });

    it('should reset error on successful reload', () => {
      // First load fails
      mockDuplicateService.getAll.mockReturnValue(
        throwError(() => new Error('Network error'))
      );

      fixture.detectChanges();

      expect(component.error()).toBe('Failed to load duplicate groups');

      // Second load succeeds
      mockDuplicateService.getAll.mockReturnValue(of(mockPagedResponse));

      component.loadGroups();

      expect(component.error()).toBeNull();
      expect(component.groups().length).toBe(3);
    });
  });

  describe('Computed Properties', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should update allSelected computed when selection changes', () => {
      expect(component.allSelected()).toBe(false);

      component.toggleSelection(mockGroup1);
      expect(component.allSelected()).toBe(false);

      component.toggleSelection(mockGroup2);
      expect(component.allSelected()).toBe(false);

      component.toggleSelection(mockGroup3);
      expect(component.allSelected()).toBe(true);
    });

    it('should update someSelected computed when selection changes', () => {
      expect(component.someSelected()).toBe(false);

      component.toggleSelection(mockGroup1);
      expect(component.someSelected()).toBe(true);

      component.toggleSelection(mockGroup2);
      component.toggleSelection(mockGroup3);
      expect(component.someSelected()).toBe(false); // All selected
      expect(component.allSelected()).toBe(true);
    });

    it('should handle empty groups array for computed properties', () => {
      component.groups.set([]);

      expect(component.allSelected()).toBe(false);
      expect(component.someSelected()).toBe(false);
    });
  });

  describe('Integration Tests', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should maintain selection across sorting', () => {
      component.toggleSelection(mockGroup1);
      component.toggleSelection(mockGroup2);

      const sortEvent: Sort = {
        active: 'fileCount',
        direction: 'asc',
      };

      component.onSortChange(sortEvent);

      expect(component.selectedGroupIds().size).toBe(2);
      expect(component.isSelected(mockGroup1)).toBe(true);
      expect(component.isSelected(mockGroup2)).toBe(true);
    });

    it('should clear selection but maintain expansion on page change', () => {
      component.toggleSelection(mockGroup1);
      component.toggleRowExpansion('group-1');

      const pageEvent: PageEvent = {
        pageIndex: 1,
        pageSize: 50,
        length: 100,
      };

      component.onPageChange(pageEvent);

      expect(component.selectedGroupIds().size).toBe(0);
      expect(component.isRowExpanded('group-1')).toBe(true); // Expansion maintained
    });

    it('should handle rapid consecutive operations', () => {
      component.toggleSelection(mockGroup1);
      component.toggleRowExpansion('group-1');
      component.toggleSelection(mockGroup2);
      component.toggleRowExpansion('group-2');
      component.toggleSelection(mockGroup1); // Deselect

      expect(component.selectedGroupIds().size).toBe(1);
      expect(component.isSelected(mockGroup2)).toBe(true);
      expect(component.isRowExpanded('group-1')).toBe(true);
      expect(component.isRowExpanded('group-2')).toBe(true);
    });
  });
});
