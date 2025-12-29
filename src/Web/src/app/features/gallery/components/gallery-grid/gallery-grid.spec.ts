import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { GalleryGridComponent } from './gallery-grid';
import { IndexedFileDto } from '../../../../models';

describe('GalleryGridComponent', () => {
  let component: GalleryGridComponent;
  let fixture: ComponentFixture<GalleryGridComponent>;

  const mockFiles: IndexedFileDto[] = [
    {
      id: '1',
      filePath: '/photos/img001.jpg',
      fileName: 'img001.jpg',
      fileHash: 'abc123',
      fileSize: 1024000,
      width: 1920,
      height: 1080,
      createdAt: '2025-12-01T00:00:00Z',
      modifiedAt: '2025-12-01T00:00:00Z',
      indexedAt: '2025-12-20T00:00:00Z',
      thumbnailPath: '/thumbnails/abc123.jpg',
      isDuplicate: false,
      duplicateGroupId: null,
      dateTaken: null,
      cameraMake: null,
      cameraModel: null,
      gpsLatitude: null,
      gpsLongitude: null,
      iso: null,
      aperture: null,
      shutterSpeed: null,
      lastError: null,
      retryCount: 0
    },
    {
      id: '2',
      filePath: '/photos/img002.jpg',
      fileName: 'img002.jpg',
      fileHash: 'def456',
      fileSize: 2048000,
      width: 1920,
      height: 1080,
      createdAt: '2025-12-02T00:00:00Z',
      modifiedAt: '2025-12-02T00:00:00Z',
      indexedAt: '2025-12-20T00:00:00Z',
      thumbnailPath: '/thumbnails/def456.jpg',
      isDuplicate: true,
      duplicateGroupId: 'group1',
      dateTaken: null,
      cameraMake: null,
      cameraModel: null,
      gpsLatitude: null,
      gpsLongitude: null,
      iso: null,
      aperture: null,
      shutterSpeed: null,
      lastError: null,
      retryCount: 0
    }
  ];

  // Mock ResizeObserver
  beforeAll(() => {
    (window as any).ResizeObserver = class ResizeObserver {
      observe() {}
      unobserve() {}
      disconnect() {}
    };
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GalleryGridComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(GalleryGridComponent);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('files', mockFiles);

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have default values', () => {
    expect(component.loading()).toBe(false);
    expect(component.loadingMore()).toBe(false);
    expect(component.hasMore()).toBe(true);
    expect(component.tileSize()).toBe(180);
  });

  it('should display files', () => {
    expect(component.files()).toEqual(mockFiles);
  });

  it('should group files into rows', () => {
    const rows = component.rows;
    expect(rows.length).toBeGreaterThan(0);
    // With 2 files and default 5 items per row, should have 1 row
    expect(rows[0].length).toBe(2);
  });

  it('should calculate row height based on tile size', () => {
    expect(component.rowHeight).toBe(184); // 180 + 4 (gap)

    fixture.componentRef.setInput('tileSize', 120);
    fixture.detectChanges();

    expect(component.rowHeight).toBe(124); // 120 + 4 (gap)
  });

  it('should emit fileClick on tile click', () => {
    const clickSpy = vi.fn();
    component.fileClick.subscribe(clickSpy);

    component.onTileClick(mockFiles[0]);

    expect(clickSpy).toHaveBeenCalledWith(mockFiles[0]);
  });

  it('should emit fileSelect on tile select', () => {
    const selectSpy = vi.fn();
    component.fileSelect.subscribe(selectSpy);

    component.onTileSelect(mockFiles[0]);

    expect(selectSpy).toHaveBeenCalledWith(mockFiles[0]);
  });

  it('should have onScroll method', () => {
    expect(typeof component.onScroll).toBe('function');
  });

  it('should not emit loadMore when already loading', () => {
    fixture.componentRef.setInput('loadingMore', true);
    fixture.detectChanges();

    const loadMoreSpy = vi.fn();
    component.loadMore.subscribe(loadMoreSpy);

    component.onScroll();

    expect(loadMoreSpy).not.toHaveBeenCalled();
  });

  it('should not emit loadMore when no more items', () => {
    fixture.componentRef.setInput('hasMore', false);
    fixture.detectChanges();

    const loadMoreSpy = vi.fn();
    component.loadMore.subscribe(loadMoreSpy);

    component.onScroll();

    expect(loadMoreSpy).not.toHaveBeenCalled();
  });

  it('should track rows by file ids', () => {
    const row = [mockFiles[0], mockFiles[1]];
    const trackResult = component.trackByRow(0, row);
    expect(trackResult).toBe('1-2');
  });

  it('should track files by id', () => {
    const trackResult = component.trackByFile(0, mockFiles[0]);
    expect(trackResult).toBe('1');
  });

  it('should show loading overlay when loading', () => {
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();

    const loadingOverlay = fixture.nativeElement.querySelector('.loading-overlay');
    expect(loadingOverlay).toBeTruthy();
  });

  it('should show empty state when no files', () => {
    fixture.componentRef.setInput('files', []);
    fixture.detectChanges();

    const emptyState = fixture.nativeElement.querySelector('.empty-state');
    expect(emptyState).toBeTruthy();
  });

  it('should show loading more indicator when loading more', () => {
    fixture.componentRef.setInput('loadingMore', true);
    fixture.detectChanges();

    const loadingMore = fixture.nativeElement.querySelector('.loading-more');
    expect(loadingMore).toBeTruthy();
  });

  it('should clean up resize observer on destroy', () => {
    component.ngAfterViewInit();
    // Just verify the component can be destroyed without errors
    expect(() => component.ngOnDestroy()).not.toThrow();
  });
});
