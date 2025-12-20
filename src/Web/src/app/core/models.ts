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
