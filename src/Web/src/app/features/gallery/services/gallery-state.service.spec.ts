import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { GalleryStateService } from './gallery-state.service';
import { IndexedFileDto, PagedResponse } from '../../../models';

describe('GalleryStateService', () => {
  let service: GalleryStateService;
  let httpMock: HttpTestingController;
  const apiUrl = 'http://localhost:5000/api/files';
  const hiddenCountUrl = 'http://localhost:5000/api/hidden-folders/hidden-count';

  const mockFile: IndexedFileDto = {
    id: '123e4567-e89b-12d3-a456-426614174000',
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
    dateTaken: '2025-12-01T10:30:00Z',
    cameraMake: null,
    cameraModel: null,
    gpsLatitude: null,
    gpsLongitude: null,
    iso: null,
    aperture: null,
    shutterSpeed: null,
    lastError: null,
    retryCount: 0,
    isHidden: false
  };

  const mockPagedResponse: PagedResponse<IndexedFileDto> = {
    items: [mockFile],
    page: 1,
    pageSize: 100,
    totalItems: 1,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [GalleryStateService, provideHttpClient(), provideHttpClientTesting()]
    });

    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(GalleryStateService);

    // Handle the hidden count request made by HiddenStateService on init
    const hiddenCountReq = httpMock.expectOne(hiddenCountUrl);
    hiddenCountReq.flush({ count: 0 });
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should have default tile size of medium', () => {
    expect(service.tileSize()).toBe('medium');
  });

  it('should compute tile size in pixels', () => {
    expect(service.tileSizePx()).toBe(180);

    service.setTileSize('small');
    expect(service.tileSizePx()).toBe(120);

    service.setTileSize('large');
    expect(service.tileSizePx()).toBe(240);
  });

  it('should have empty files initially', () => {
    expect(service.files()).toEqual([]);
    expect(service.loading()).toBe(false);
    expect(service.loadingMore()).toBe(false);
  });

  it('should have default filters', () => {
    const filters = service.filters();
    expect(filters.directory).toBeNull();
    expect(filters.search).toBeNull();
    expect(filters.minDate).toBeNull();
    expect(filters.maxDate).toBeNull();
    expect(filters.duplicatesOnly).toBe(false);
  });

  it('should set loading to true when loading files', async () => {
    const loadPromise = service.loadFiles();
    expect(service.loading()).toBe(true);

    const req = httpMock.expectOne((request) => request.url === apiUrl);
    req.flush(mockPagedResponse);
    await loadPromise;

    expect(service.loading()).toBe(false);
    expect(service.files().length).toBe(1);
    expect(service.files()[0].fileName).toBe('img001.jpg');
  });

  it('should set tile size', () => {
    service.setTileSize('large');
    expect(service.tileSize()).toBe('large');
  });

  it('should get thumbnail URL', () => {
    const file = { ...mockFile, thumbnailPath: 'thumbnails/test.jpg' };
    const url = service.getThumbnailUrl(file);
    expect(url).toContain('thumbnails');
  });

  it('should compute files by date', () => {
    // Manually set files signal for testing computed
    service['files'].set([
      { ...mockFile, id: '1', dateTaken: '2025-12-01T10:30:00Z' },
      { ...mockFile, id: '2', dateTaken: '2025-12-01T15:00:00Z' },
      { ...mockFile, id: '3', dateTaken: '2025-12-02T09:00:00Z' }
    ]);

    const byDate = service.filesByDate();
    expect(byDate.length).toBe(2);
    expect(byDate[0].date).toBe('2025-12-02'); // Most recent first
    expect(byDate[0].files.length).toBe(1);
    expect(byDate[1].date).toBe('2025-12-01');
    expect(byDate[1].files.length).toBe(2);
  });

  it('should update totalItems after loading', async () => {
    const loadPromise = service.loadFiles();

    const req = httpMock.expectOne((request) => request.url === apiUrl);
    req.flush({ ...mockPagedResponse, totalItems: 100 });
    await loadPromise;

    expect(service.totalItems()).toBe(100);
  });

  it('should update hasMore based on response', async () => {
    const loadPromise = service.loadFiles();

    const req = httpMock.expectOne((request) => request.url === apiUrl);
    req.flush({ ...mockPagedResponse, hasNextPage: true });
    await loadPromise;

    expect(service.hasMore()).toBe(true);
  });
});
