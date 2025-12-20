import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ScanDirectoryService } from './scan-directory.service';
import { ScanDirectoryDto, CreateScanDirectoryRequest, UpdateScanDirectoryRequest } from '../models';

describe('ScanDirectoryService', () => {
  let service: ScanDirectoryService;
  let httpMock: HttpTestingController;
  const apiUrl = 'http://localhost:5000/api/scan-directories';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ScanDirectoryService, provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(ScanDirectoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should get all scan directories', () => {
    const mockDirectories: ScanDirectoryDto[] = [
      {
        id: '123e4567-e89b-12d3-a456-426614174000',
        path: '/photos',
        isEnabled: true,
        lastScannedAt: '2025-12-20T00:00:00Z',
        createdAt: '2025-12-19T00:00:00Z',
        fileCount: 100
      }
    ];

    service.getAll().subscribe((directories) => {
      expect(directories.length).toBe(1);
      expect(directories[0].path).toBe('/photos');
    });

    const req = httpMock.expectOne(apiUrl);
    expect(req.request.method).toBe('GET');
    req.flush(mockDirectories);
  });

  it('should get directory by id', () => {
    const mockDirectory: ScanDirectoryDto = {
      id: '123e4567-e89b-12d3-a456-426614174000',
      path: '/photos',
      isEnabled: true,
      lastScannedAt: null,
      createdAt: '2025-12-19T00:00:00Z',
      fileCount: 0
    };

    service.getById('123e4567-e89b-12d3-a456-426614174000').subscribe((directory) => {
      expect(directory.path).toBe('/photos');
    });

    const req = httpMock.expectOne(`${apiUrl}/123e4567-e89b-12d3-a456-426614174000`);
    expect(req.request.method).toBe('GET');
    req.flush(mockDirectory);
  });

  it('should create a new scan directory', () => {
    const request: CreateScanDirectoryRequest = {
      path: '/new-photos',
      isEnabled: true
    };

    const mockResponse: ScanDirectoryDto = {
      id: '123e4567-e89b-12d3-a456-426614174001',
      path: '/new-photos',
      isEnabled: true,
      lastScannedAt: null,
      createdAt: '2025-12-20T00:00:00Z',
      fileCount: 0
    };

    service.create(request).subscribe((directory) => {
      expect(directory.path).toBe('/new-photos');
      expect(directory.id).toBe('123e4567-e89b-12d3-a456-426614174001');
    });

    const req = httpMock.expectOne(apiUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(mockResponse);
  });

  it('should update a scan directory', () => {
    const id = '123e4567-e89b-12d3-a456-426614174000';
    const request: UpdateScanDirectoryRequest = {
      isEnabled: false
    };

    const mockResponse: ScanDirectoryDto = {
      id,
      path: '/photos',
      isEnabled: false,
      lastScannedAt: null,
      createdAt: '2025-12-19T00:00:00Z',
      fileCount: 0
    };

    service.update(id, request).subscribe((directory) => {
      expect(directory.isEnabled).toBe(false);
    });

    const req = httpMock.expectOne(`${apiUrl}/${id}`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(mockResponse);
  });

  it('should delete a scan directory', () => {
    const id = '123e4567-e89b-12d3-a456-426614174000';

    service.delete(id).subscribe();

    const req = httpMock.expectOne(`${apiUrl}/${id}`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('should trigger scan for a directory', () => {
    const id = '123e4567-e89b-12d3-a456-426614174000';

    service.triggerScan(id).subscribe();

    const req = httpMock.expectOne(`${apiUrl}/${id}/scan`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(null);
  });
});
