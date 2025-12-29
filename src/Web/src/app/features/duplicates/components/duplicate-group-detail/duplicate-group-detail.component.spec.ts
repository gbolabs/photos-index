import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { DuplicateGroupDetailComponent } from './duplicate-group-detail.component';
import { DuplicateService } from '../../../../services/duplicate.service';
import { DuplicateGroupDto, IndexedFileDto } from '../../../../models';

describe('DuplicateGroupDetailComponent', () => {
  let component: DuplicateGroupDetailComponent;
  let fixture: ComponentFixture<DuplicateGroupDetailComponent>;
  let duplicateService: DuplicateService;

  const mockFile1: IndexedFileDto = {
    id: 'file-1',
    filePath: '/test/image1.jpg',
    fileName: 'image1.jpg',
    fileHash: 'abc123',
    fileSize: 1024000,
    width: 1920,
    height: 1080,
    createdAt: '2024-01-01T10:00:00Z',
    modifiedAt: '2024-01-01T10:00:00Z',
    indexedAt: '2024-01-01T10:00:00Z',
    thumbnailPath: null,
    isDuplicate: true,
    duplicateGroupId: 'group-1',
    dateTaken: null,
    cameraMake: null,
    cameraModel: null,
    gpsLatitude: null,
    gpsLongitude: null,
    iso: null,
    aperture: null,
    shutterSpeed: null,
    lastError: null,
    retryCount: 0,
    isHidden: false,
  };

  const mockFile2: IndexedFileDto = {
    id: 'file-2',
    filePath: '/test/image2.jpg',
    fileName: 'image2.jpg',
    fileHash: 'abc123',
    fileSize: 1024000,
    width: 1920,
    height: 1080,
    createdAt: '2024-01-01T10:00:00Z',
    modifiedAt: '2024-01-01T10:00:00Z',
    indexedAt: '2024-01-01T10:00:00Z',
    thumbnailPath: null,
    isDuplicate: true,
    duplicateGroupId: 'group-1',
    dateTaken: null,
    cameraMake: null,
    cameraModel: null,
    gpsLatitude: null,
    gpsLongitude: null,
    iso: null,
    aperture: null,
    shutterSpeed: null,
    lastError: null,
    retryCount: 0,
    isHidden: false,
  };

  const mockGroup: DuplicateGroupDto = {
    id: 'group-1',
    hash: 'abc123',
    fileCount: 2,
    totalSize: 2048000,
    potentialSavings: 1024000,
    originalFileId: 'file-1',
    files: [mockFile1, mockFile2],
    resolvedAt: null,
    createdAt: '2024-01-01T10:00:00Z',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DuplicateGroupDetailComponent],
      providers: [
        DuplicateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DuplicateGroupDetailComponent);
    component = fixture.componentInstance;
    duplicateService = TestBed.inject(DuplicateService);
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load group on ngOnChanges when groupId is provided', () => {
    vi.spyOn(duplicateService, 'getById').mockReturnValue(of(mockGroup));
    
    fixture.componentRef.setInput('groupId', 'group-1');
    component.ngOnChanges({
      groupId: {
        currentValue: 'group-1',
        previousValue: undefined,
        firstChange: true,
        isFirstChange: () => true,
      },
    });

    expect(duplicateService.getById).toHaveBeenCalledWith('group-1');
    expect(component.group()).toEqual(mockGroup);
    expect(component.loading()).toBe(false);
  });

  it('should handle error when loading group fails', () => {
    vi.spyOn(duplicateService, 'getById').mockReturnValue(
      throwError(() => new Error('API Error'))
    );

    fixture.componentRef.setInput('groupId', 'group-1');
    component.ngOnChanges({
      groupId: {
        currentValue: 'group-1',
        previousValue: undefined,
        firstChange: true,
        isFirstChange: () => true,
      },
    });

    expect(component.error()).toBeTruthy();
    expect(component.loading()).toBe(false);
  });

  it('should compute files from group', () => {
    component.group.set(mockGroup);
    expect(component.files()).toEqual([mockFile1, mockFile2]);
  });

  it('should compute originalFile when originalFileId is set', () => {
    component.group.set(mockGroup);
    expect(component.originalFile()).toEqual(mockFile1);
  });

  it('should compute isResolved correctly', () => {
    const resolvedGroup = { ...mockGroup, resolvedAt: '2024-01-02T10:00:00Z' };
    component.group.set(resolvedGroup);
    expect(component.isResolved()).toBe(true);

    const unresolvedGroup = { ...mockGroup, resolvedAt: null };
    component.group.set(unresolvedGroup);
    expect(component.isResolved()).toBe(false);
  });

  it('should emit back event when goBack is called', () => {
    let backEmitted = false;
    component.back.subscribe(() => {
      backEmitted = true;
    });

    component.goBack();
    expect(backEmitted).toBe(true);
  });

  it('should set selected file', () => {
    component.selectFile(mockFile1);
    expect(component.selectedFileId()).toBe('file-1');
    expect(component.isFileSelected(mockFile1)).toBe(true);
  });

  it('should identify original file correctly', () => {
    component.group.set(mockGroup);
    expect(component.isOriginal(mockFile1)).toBe(true);
    expect(component.isOriginal(mockFile2)).toBe(false);
  });

  it('should set file as original', () => {
    vi.spyOn(duplicateService, 'setOriginal').mockReturnValue(of(undefined));
    vi.spyOn(component, 'loadGroup');

    component.group.set(mockGroup);
    component.setAsOriginal(mockFile2);

    expect(duplicateService.setOriginal).toHaveBeenCalledWith('group-1', 'file-2');
    expect(component.loadGroup).toHaveBeenCalled();
  });

  it('should auto-select original', () => {
    vi.spyOn(duplicateService, 'autoSelect').mockReturnValue(of({ originalFileId: 'file-1' }));
    vi.spyOn(component, 'loadGroup');

    component.group.set(mockGroup);
    component.autoSelectOriginal();

    expect(duplicateService.autoSelect).toHaveBeenCalledWith('group-1');
    expect(component.loadGroup).toHaveBeenCalled();
  });

  it('should toggle comparison mode', () => {
    component.toggleComparison(mockFile1);
    expect(component.comparisonFiles()).toContain(mockFile1);
    expect(component.isInComparison(mockFile1)).toBe(true);

    component.toggleComparison(mockFile2);
    expect(component.comparisonFiles()).toContain(mockFile2);
    expect(component.comparisonMode()).toBe(true);

    component.toggleComparison(mockFile1);
    expect(component.comparisonFiles()).not.toContain(mockFile1);
  });

  it('should limit comparison to 2 files', () => {
    component.toggleComparison(mockFile1);
    component.toggleComparison(mockFile2);

    const mockFile3: IndexedFileDto = { ...mockFile1, id: 'file-3' };
    component.toggleComparison(mockFile3);

    expect(component.comparisonFiles().length).toBe(2);
  });

  it('should clear comparison', () => {
    component.toggleComparison(mockFile1);
    component.toggleComparison(mockFile2);
    component.clearComparison();

    expect(component.comparisonFiles().length).toBe(0);
    expect(component.comparisonMode()).toBe(false);
  });

  it('should format date correctly', () => {
    const result = component.formatDate('2024-01-15T10:30:00Z');
    expect(result).toContain('2024');
  });

  it('should handle null date', () => {
    const result = component.formatDate(null);
    expect(result).toBe('-');
  });

  it('should format dimensions', () => {
    const result = component.formatDimensions(mockFile1);
    expect(result).toBe('1920 x 1080');
  });

  it('should handle missing dimensions', () => {
    const fileWithoutDimensions = { ...mockFile1, width: null, height: null };
    const result = component.formatDimensions(fileWithoutDimensions);
    expect(result).toBe('-');
  });

  it('should get file name from path', () => {
    const result = component.getFileName('/test/path/image.jpg');
    expect(result).toBe('image.jpg');
  });

  it('should get directory from path', () => {
    const result = component.getDirectory('/test/path/image.jpg');
    expect(result).toBe('/test/path');
  });

  it('should get thumbnail URL', () => {
    vi.spyOn(duplicateService, 'getThumbnailUrl').mockReturnValue('/api/thumbnails/file-1');
    const result = component.getThumbnailUrl(mockFile1);
    expect(result).toBe('/api/thumbnails/file-1');
  });

  it('should get download URL', () => {
    vi.spyOn(duplicateService, 'getDownloadUrl').mockReturnValue('/api/download/file-1');
    const result = component.getDownloadUrl(mockFile1);
    expect(result).toBe('/api/download/file-1');
  });
});
