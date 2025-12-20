/**
 * Shared TypeScript models matching the API DTOs
 */

export interface ScanDirectoryDto {
  id: string;
  path: string;
  isEnabled: boolean;
  lastScannedAt: string | null;
  createdAt: string;
  fileCount: number;
}

export interface FileStatisticsDto {
  totalFiles: number;
  totalSizeBytes: number;
  duplicateGroups: number;
  duplicateFiles: number;
  potentialSavingsBytes: number;
  lastIndexedAt: string | null;
}

export interface CreateScanDirectoryRequest {
  path: string;
  isEnabled: boolean;
}

export interface UpdateScanDirectoryRequest {
  path?: string;
  isEnabled?: boolean;
}

export interface ApiErrorResponse {
  message: string;
  details?: string;
}

/**
 * Generic paged response wrapper.
 * Matches backend: src/Shared/Responses/PagedResponse.cs
 */
export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
