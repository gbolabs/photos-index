import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { DuplicateService } from './duplicate.service';
import { DuplicateGroupDto, PagedResponse, FileStatisticsDto } from '../models';

describe('DuplicateService', () => {
  let service: DuplicateService;
  let httpMock: HttpTestingController;
  const apiUrl = 'http://localhost:5000/api/duplicates';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        DuplicateService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(DuplicateService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should get all duplicate groups with pagination', () => {
    const mockResponse: PagedResponse<DuplicateGroupDto> = {
      items: [],
      page: 1,
      pageSize: 20,
      totalItems: 0,
      totalPages: 0,
      hasNextPage: false,
      hasPreviousPage: false,
    };

    service.getAll(1, 20).subscribe((response) => {
      expect(response.page).toBe(1);
      expect(response.pageSize).toBe(20);
    });

    const req = httpMock.expectOne((request) => {
      return (
        request.url === apiUrl &&
        request.params.get('page') === '1' &&
        request.params.get('pageSize') === '20'
      );
    });
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('should get duplicate group by id', () => {
    const mockGroup: DuplicateGroupDto = {
      id: '123e4567-e89b-12d3-a456-426614174000',
      hash: 'abc123',
      fileCount: 3,
      totalSize: 3072000,
      potentialSavings: 2048000,
      resolvedAt: null,
      createdAt: '2025-12-20T00:00:00Z',
      originalFileId: null,
      files: [],
      firstFileThumbnailPath: null,
      status: 'pending',
      validatedAt: null,
      keptFileId: null,
      lastReviewedAt: null,
      reviewOrder: null,
      reviewSessionId: null,
    };

    service
      .getById('123e4567-e89b-12d3-a456-426614174000')
      .subscribe((group) => {
        expect(group.hash).toBe('abc123');
        expect(group.fileCount).toBe(3);
      });

    const req = httpMock.expectOne(
      `${apiUrl}/123e4567-e89b-12d3-a456-426614174000`
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockGroup);
  });

  it('should set original file in duplicate group', () => {
    const groupId = '123e4567-e89b-12d3-a456-426614174000';
    const fileId = '123e4567-e89b-12d3-a456-426614174001';

    service.setOriginal(groupId, fileId).subscribe();

    const req = httpMock.expectOne(`${apiUrl}/${groupId}/original`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ fileId });
    req.flush(null);
  });

  it('should auto-select original file', () => {
    const groupId = '123e4567-e89b-12d3-a456-426614174000';

    const mockResponse = {
      originalFileId: '123e4567-e89b-12d3-a456-426614174001',
    };

    service.autoSelect(groupId).subscribe((response) => {
      expect(response.originalFileId).toBe(
        '123e4567-e89b-12d3-a456-426614174001'
      );
    });

    const req = httpMock.expectOne(`${apiUrl}/${groupId}/auto-select`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(mockResponse);
  });

  it('should auto-select all originals', () => {
    const mockResponse = { groupsProcessed: 5 };

    service.autoSelectAll().subscribe((response) => {
      expect(response.groupsProcessed).toBe(5);
    });

    const req = httpMock.expectOne(`${apiUrl}/auto-select-all`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(mockResponse);
  });

  it('should get duplicate statistics', () => {
    const mockStats: FileStatisticsDto = {
      totalFiles: 100,
      totalSizeBytes: 1000000,
      duplicateGroups: 10,
      duplicateFiles: 25,
      potentialSavingsBytes: 500000,
      lastIndexedAt: '2025-12-20T10:00:00Z',
    };

    service.getStatistics().subscribe((stats) => {
      expect(stats.duplicateGroups).toBe(10);
      expect(stats.potentialSavingsBytes).toBe(500000);
    });

    const req = httpMock.expectOne(`${apiUrl}/stats`);
    expect(req.request.method).toBe('GET');
    req.flush(mockStats);
  });

  it('should delete non-originals in group', () => {
    const groupId = '123e4567-e89b-12d3-a456-426614174000';
    const mockResponse = { filesQueued: 2 };

    service.deleteNonOriginals(groupId).subscribe((response) => {
      expect(response.filesQueued).toBe(2);
    });

    const req = httpMock.expectOne(`${apiUrl}/${groupId}/non-originals`);
    expect(req.request.method).toBe('DELETE');
    req.flush(mockResponse);
  });

  it('should return correct thumbnail URL via API when no thumbnailPath', () => {
    const fileId = '123e4567-e89b-12d3-a456-426614174000';
    const url = service.getThumbnailUrl(fileId);
    expect(url).toBe(`http://localhost:5000/api/files/${fileId}/thumbnail`);
  });

  it('should return direct MinIO URL when thumbnailPath is provided', () => {
    const fileId = '123e4567-e89b-12d3-a456-426614174000';
    const thumbnailPath = 'thumbnails/abc123_thumb.jpg';
    const url = service.getThumbnailUrl(fileId, thumbnailPath);
    expect(url).toBe(`/thumbnails/${thumbnailPath}`);
  });

  it('should return correct download URL', () => {
    const fileId = '123e4567-e89b-12d3-a456-426614174000';
    const url = service.getDownloadUrl(fileId);
    expect(url).toBe(`http://localhost:5000/api/files/${fileId}/download`);
  });
});
