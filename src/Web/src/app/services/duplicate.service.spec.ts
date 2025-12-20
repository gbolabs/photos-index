import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DuplicateService } from './duplicate.service';
import { DuplicateGroupDto, SetOriginalRequest, PagedResponse } from '../models';

describe('DuplicateService', () => {
  let service: DuplicateService;
  let httpMock: HttpTestingController;
  const apiUrl = 'http://localhost:5000/api/duplicates';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [DuplicateService, provideHttpClient(), provideHttpClientTesting()]
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
      pageSize: 50,
      totalItems: 0,
      totalPages: 0,
      hasNextPage: false,
      hasPreviousPage: false
    };

    service.getAll(1, 50, false).subscribe((response) => {
      expect(response.page).toBe(1);
    });

    const req = httpMock.expectOne((request) => {
      return (
        request.url === apiUrl &&
        request.params.get('page') === '1' &&
        request.params.get('pageSize') === '50' &&
        request.params.get('resolved') === 'false'
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
      files: []
    };

    service.getById('123e4567-e89b-12d3-a456-426614174000').subscribe((group) => {
      expect(group.hash).toBe('abc123');
      expect(group.fileCount).toBe(3);
    });

    const req = httpMock.expectOne(`${apiUrl}/123e4567-e89b-12d3-a456-426614174000`);
    expect(req.request.method).toBe('GET');
    req.flush(mockGroup);
  });

  it('should set original file in duplicate group', () => {
    const groupId = '123e4567-e89b-12d3-a456-426614174000';
    const request: SetOriginalRequest = {
      fileId: '123e4567-e89b-12d3-a456-426614174001'
    };

    const mockResponse: DuplicateGroupDto = {
      id: groupId,
      hash: 'abc123',
      fileCount: 3,
      totalSize: 3072000,
      potentialSavings: 2048000,
      resolvedAt: null,
      createdAt: '2025-12-20T00:00:00Z',
      originalFileId: '123e4567-e89b-12d3-a456-426614174001',
      files: []
    };

    service.setOriginal(groupId, request).subscribe((group) => {
      expect(group.originalFileId).toBe('123e4567-e89b-12d3-a456-426614174001');
    });

    const req = httpMock.expectOne(`${apiUrl}/${groupId}/set-original`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(mockResponse);
  });

  it('should auto-select original file', () => {
    const groupId = '123e4567-e89b-12d3-a456-426614174000';

    const mockResponse: DuplicateGroupDto = {
      id: groupId,
      hash: 'abc123',
      fileCount: 3,
      totalSize: 3072000,
      potentialSavings: 2048000,
      resolvedAt: null,
      createdAt: '2025-12-20T00:00:00Z',
      originalFileId: '123e4567-e89b-12d3-a456-426614174001',
      files: []
    };

    service.autoSelect(groupId).subscribe((group) => {
      expect(group.originalFileId).toBeTruthy();
    });

    const req = httpMock.expectOne(`${apiUrl}/${groupId}/auto-select`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(mockResponse);
  });

  it('should resolve duplicate group', () => {
    const groupId = '123e4567-e89b-12d3-a456-426614174000';

    const mockResponse: DuplicateGroupDto = {
      id: groupId,
      hash: 'abc123',
      fileCount: 3,
      totalSize: 3072000,
      potentialSavings: 2048000,
      resolvedAt: '2025-12-20T10:00:00Z',
      createdAt: '2025-12-20T00:00:00Z',
      originalFileId: '123e4567-e89b-12d3-a456-426614174001',
      files: []
    };

    service.resolve(groupId).subscribe((group) => {
      expect(group.resolvedAt).toBeTruthy();
    });

    const req = httpMock.expectOne(`${apiUrl}/${groupId}/resolve`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(mockResponse);
  });

  it('should delete duplicates in group', () => {
    const groupId = '123e4567-e89b-12d3-a456-426614174000';

    service.deleteDuplicates(groupId).subscribe();

    const req = httpMock.expectOne(`${apiUrl}/${groupId}/duplicates`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('should get unresolved duplicate groups', () => {
    const mockResponse: PagedResponse<DuplicateGroupDto> = {
      items: [],
      page: 1,
      pageSize: 50,
      totalItems: 0,
      totalPages: 0,
      hasNextPage: false,
      hasPreviousPage: false
    };

    service.getUnresolved(1, 50).subscribe();

    const req = httpMock.expectOne((request) => {
      return request.params.get('resolved') === 'false';
    });
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });

  it('should get resolved duplicate groups', () => {
    const mockResponse: PagedResponse<DuplicateGroupDto> = {
      items: [],
      page: 1,
      pageSize: 50,
      totalItems: 0,
      totalPages: 0,
      hasNextPage: false,
      hasPreviousPage: false
    };

    service.getResolved(1, 50).subscribe();

    const req = httpMock.expectOne((request) => {
      return request.params.get('resolved') === 'true';
    });
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });
});
