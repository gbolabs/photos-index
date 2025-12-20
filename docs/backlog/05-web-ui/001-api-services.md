# 001: Angular API Services

**Status**: ✅ Complete
**PR**: [#8](https://github.com/gbolabs/photos-index/pull/8)
**Priority**: P2 (User Interface)
**Agent**: A4
**Branch**: `feature/web-api-services`
**Estimated Complexity**: Medium

## Objective

Implement typed Angular services for communicating with the backend API, including error handling, loading states, and caching.

## Dependencies

- `02-api-layer/001-scan-directories.md` (API contract)
- `02-api-layer/002-indexed-files.md` (API contract)
- `02-api-layer/003-duplicate-groups.md` (API contract)

## Acceptance Criteria

- [ ] ScanDirectoryService with full CRUD operations
- [ ] IndexedFileService with query, pagination, thumbnails
- [ ] DuplicateService for duplicate group management
- [ ] Typed DTOs matching backend contracts
- [ ] Error handling with user-friendly messages
- [ ] Loading state management
- [ ] Response caching where appropriate
- [ ] Unit tests with HttpClientTestingModule

## TDD Steps

### Red Phase - Service Tests
```typescript
// src/app/services/scan-directory.service.spec.ts
describe('ScanDirectoryService', () => {
  let service: ScanDirectoryService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [ScanDirectoryService]
    });
    service = TestBed.inject(ScanDirectoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('should fetch all directories', () => {
    const mockResponse: PagedResponse<ScanDirectoryDto> = {
      items: [{ id: '123', path: '/photos', isEnabled: true }],
      page: 1,
      pageSize: 10,
      totalItems: 1,
      totalPages: 1
    };

    service.getAll().subscribe(result => {
      expect(result.items.length).toBe(1);
    });

    const req = httpMock.expectOne('/api/scan-directories?page=1&pageSize=50');
    req.flush(mockResponse);
  });

  it('should handle errors gracefully', () => {
    service.getAll().subscribe({
      error: (err) => {
        expect(err.message).toBe('Failed to load directories');
      }
    });

    const req = httpMock.expectOne('/api/scan-directories?page=1&pageSize=50');
    req.error(new ErrorEvent('Network error'));
  });
});
```

### Green Phase
Implement services.

### Refactor Phase
Add caching, optimize error handling.

## Files to Create

```
src/Web/src/app/
├── models/
│   ├── scan-directory.dto.ts
│   ├── indexed-file.dto.ts
│   ├── duplicate-group.dto.ts
│   ├── paged-response.ts
│   └── api-error.ts
├── services/
│   ├── scan-directory.service.ts
│   ├── scan-directory.service.spec.ts
│   ├── indexed-file.service.ts
│   ├── indexed-file.service.spec.ts
│   ├── duplicate.service.ts
│   ├── duplicate.service.spec.ts
│   └── api-error-handler.ts
└── state/
    ├── loading.service.ts
    └── notification.service.ts
```

## DTO Definitions

```typescript
// models/scan-directory.dto.ts
export interface ScanDirectoryDto {
  id: string;
  path: string;
  isEnabled: boolean;
  includeSubdirectories: boolean;
  lastScanUtc: string | null;
  fileCount: number;
  totalSizeBytes: number;
}

export interface CreateScanDirectoryRequest {
  path: string;
  isEnabled: boolean;
  includeSubdirectories: boolean;
}

// models/indexed-file.dto.ts
export interface IndexedFileDto {
  id: string;
  filePath: string;
  fileName: string;
  sha256Hash: string;
  fileSizeBytes: number;
  width: number | null;
  height: number | null;
  dateTaken: string | null;
  fileModifiedUtc: string;
  indexedAtUtc: string;
  duplicateGroupId: string | null;
  isOriginal: boolean;
}

// models/paged-response.ts
export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
```

## Service Implementation

```typescript
// services/scan-directory.service.ts
@Injectable({ providedIn: 'root' })
export class ScanDirectoryService {
  private readonly apiUrl = '/api/scan-directories';

  constructor(
    private http: HttpClient,
    private errorHandler: ApiErrorHandler
  ) {}

  getAll(page = 1, pageSize = 50): Observable<PagedResponse<ScanDirectoryDto>> {
    return this.http.get<PagedResponse<ScanDirectoryDto>>(
      `${this.apiUrl}?page=${page}&pageSize=${pageSize}`
    ).pipe(
      catchError(err => this.errorHandler.handle(err, 'Failed to load directories'))
    );
  }

  getById(id: string): Observable<ScanDirectoryDto> {
    return this.http.get<ScanDirectoryDto>(`${this.apiUrl}/${id}`).pipe(
      catchError(err => this.errorHandler.handle(err, 'Failed to load directory'))
    );
  }

  create(request: CreateScanDirectoryRequest): Observable<ScanDirectoryDto> {
    return this.http.post<ScanDirectoryDto>(this.apiUrl, request).pipe(
      catchError(err => this.errorHandler.handle(err, 'Failed to create directory'))
    );
  }

  update(id: string, request: Partial<ScanDirectoryDto>): Observable<ScanDirectoryDto> {
    return this.http.put<ScanDirectoryDto>(`${this.apiUrl}/${id}`, request).pipe(
      catchError(err => this.errorHandler.handle(err, 'Failed to update directory'))
    );
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(
      catchError(err => this.errorHandler.handle(err, 'Failed to delete directory'))
    );
  }

  triggerScan(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/trigger-scan`, {}).pipe(
      catchError(err => this.errorHandler.handle(err, 'Failed to trigger scan'))
    );
  }
}
```

## Error Handler

```typescript
// services/api-error-handler.ts
@Injectable({ providedIn: 'root' })
export class ApiErrorHandler {
  constructor(private notification: NotificationService) {}

  handle(error: HttpErrorResponse, defaultMessage: string): Observable<never> {
    let message = defaultMessage;

    if (error.error?.message) {
      message = error.error.message;
    } else if (error.status === 0) {
      message = 'Cannot connect to server';
    } else if (error.status === 404) {
      message = 'Resource not found';
    }

    this.notification.error(message);
    return throwError(() => new Error(message));
  }
}
```

## Test Coverage

- All services: 90% minimum
- Error handling: 100%
- Edge cases (network errors, 404s, etc.): 100%

## Completion Checklist

- [ ] Create all DTO interfaces matching backend
- [ ] Create PagedResponse generic interface
- [ ] Implement ScanDirectoryService with all methods
- [ ] Implement IndexedFileService with query params
- [ ] Implement DuplicateService
- [ ] Create ApiErrorHandler service
- [ ] Create LoadingService for state management
- [ ] Create NotificationService for user feedback
- [ ] Write unit tests for all services
- [ ] Configure HttpClient base URL from environment
- [ ] All tests passing with coverage met
- [ ] PR created and reviewed
