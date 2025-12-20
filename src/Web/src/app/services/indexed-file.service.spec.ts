import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { IndexedFileService } from './indexed-file.service';
import {
  IndexedFileDto,
  FileQueryParameters,
  FileStatisticsDto,
  PagedResponse,
  FileSortBy
} from '../models';

describe('IndexedFileService', () => {
  let service: IndexedFileService;
  let httpMock: HttpTestingController;
  const apiUrl = 'http://localhost:5000/api/files';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [IndexedFileService, provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(IndexedFileService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should query files with default parameters', () => {
    const mockResponse: PagedResponse<IndexedFileDto> = {
      items: [],
      page: 1,
      pageSize: 50,
      totalItems: 0,
      totalPages: 0,
      hasNextPage: false,
      hasPreviousPage: false
    };

    service.query().subscribe((response) => {
      expect(response.page).toBe(1);
      expect(response.pageSize).toBe(50);
    });

    const req = httpMock.expectOne(apiUrl);
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('should query files with custom parameters', () => {
    const params: FileQueryParameters = {
      page: 2,
      pageSize: 25,
      directoryId: '123e4567-e89b-12d3-a456-426614174000',
      hasDuplicates: true,
      search: 'vacation',
      sortBy: FileSortBy.Size,
      sortDescending: false
    };

    const mockResponse: PagedResponse<IndexedFileDto> = {
      items: [],
      page: 2,
      pageSize: 25,
      totalItems: 100,
      totalPages: 4,
      hasNextPage: true,
      hasPreviousPage: true
    };

    service.query(params).subscribe((response) => {
      expect(response.page).toBe(2);
    });

    const req = httpMock.expectOne((request) => {
      return (
        request.url === apiUrl &&
        request.params.get('page') === '2' &&
        request.params.get('pageSize') === '25' &&
        request.params.get('directoryId') === '123e4567-e89b-12d3-a456-426614174000' &&
        request.params.get('hasDuplicates') === 'true' &&
        request.params.get('search') === 'vacation' &&
        request.params.get('sortBy') === 'Size' &&
        request.params.get('sortDescending') === 'false'
      );
    });
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('should get file by id', () => {
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
      duplicateGroupId: null
    };

    service.getById('123e4567-e89b-12d3-a456-426614174000').subscribe((file) => {
      expect(file.fileName).toBe('img001.jpg');
      expect(file.width).toBe(1920);
    });

    const req = httpMock.expectOne(`${apiUrl}/123e4567-e89b-12d3-a456-426614174000`);
    expect(req.request.method).toBe('GET');
    req.flush(mockFile);
  });

  it('should get file statistics', () => {
    const mockStats: FileStatisticsDto = {
      totalFiles: 1000,
      totalSizeBytes: 5000000000,
      duplicateGroups: 50,
      duplicateFiles: 150,
      potentialSavingsBytes: 500000000,
      lastIndexedAt: '2025-12-20T00:00:00Z'
    };

    service.getStatistics().subscribe((stats) => {
      expect(stats.totalFiles).toBe(1000);
      expect(stats.duplicateGroups).toBe(50);
    });

    const req = httpMock.expectOne(`${apiUrl}/statistics`);
    expect(req.request.method).toBe('GET');
    req.flush(mockStats);
  });

  it('should generate correct thumbnail URL', () => {
    const fileId = '123e4567-e89b-12d3-a456-426614174000';
    const url = service.getThumbnailUrl(fileId);

    expect(url).toBe(`${apiUrl}/${fileId}/thumbnail`);
  });

  it('should download file as blob', () => {
    const fileId = '123e4567-e89b-12d3-a456-426614174000';
    const mockBlob = new Blob(['file content'], { type: 'image/jpeg' });

    service.downloadFile(fileId).subscribe((blob) => {
      expect(blob.type).toBe('image/jpeg');
    });

    const req = httpMock.expectOne(`${apiUrl}/${fileId}/download`);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(mockBlob);
  });

  it('should delete file', () => {
    const fileId = '123e4567-e89b-12d3-a456-426614174000';

    service.delete(fileId).subscribe();

    const req = httpMock.expectOne(`${apiUrl}/${fileId}`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('should handle query parameters with date filters', () => {
    const params: FileQueryParameters = {
      minDate: '2025-01-01T00:00:00Z',
      maxDate: '2025-12-31T23:59:59Z'
    };

    const mockResponse: PagedResponse<IndexedFileDto> = {
      items: [],
      page: 1,
      pageSize: 50,
      totalItems: 0,
      totalPages: 0,
      hasNextPage: false,
      hasPreviousPage: false
    };

    service.query(params).subscribe();

    const req = httpMock.expectOne((request) => {
      return (
        request.url === apiUrl &&
        request.params.get('minDate') === '2025-01-01T00:00:00Z' &&
        request.params.get('maxDate') === '2025-12-31T23:59:59Z'
      );
    });
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });
});
