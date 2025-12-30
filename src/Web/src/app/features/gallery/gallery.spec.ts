import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { GalleryComponent } from './gallery';
import { GalleryStateService } from './services/gallery-state.service';
import { IndexedFileDto, PagedResponse } from '../../models';

describe('GalleryComponent', () => {
  let component: GalleryComponent;
  let fixture: ComponentFixture<GalleryComponent>;
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
    isDeleted: false
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
      imports: [GalleryComponent],
      providers: [
        GalleryStateService,
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(GalleryComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  // Helper to handle initial HTTP requests after detectChanges
  function handleInitialRequests(): void {
    // Handle hidden count request from HiddenStateService
    const hiddenReq = httpMock.expectOne(hiddenCountUrl);
    hiddenReq.flush({ count: 0 });
    // Handle files request from GalleryComponent ngOnInit
    const filesReq = httpMock.expectOne((request) => request.url === apiUrl);
    filesReq.flush(mockPagedResponse);
  }

  it('should create', async () => {
    fixture.detectChanges();
    handleInitialRequests();

    expect(component).toBeTruthy();
  });

  it('should expose state service signals', async () => {
    fixture.detectChanges();
    handleInitialRequests();

    expect(component.files).toBeDefined();
    expect(component.loading).toBeDefined();
    expect(component.loadingMore).toBeDefined();
    expect(component.hasMore).toBeDefined();
    expect(component.filters).toBeDefined();
    expect(component.tileSize).toBeDefined();
    expect(component.tileSizePx).toBeDefined();
  });

  it('should change tile size through state service', async () => {
    fixture.detectChanges();
    handleInitialRequests();

    component.onTileSizeChange('large');

    expect(component.tileSize()).toBe('large');
    expect(component.tileSizePx()).toBe(240);
  });

  it('should have directories signal', async () => {
    fixture.detectChanges();
    handleInitialRequests();

    expect(component.directories()).toEqual([]);
  });
});
