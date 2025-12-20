/**
 * Data transfer object for scan directory configuration.
 * Matches backend: src/Shared/Dtos/ScanDirectoryDto.cs
 */
export interface ScanDirectoryDto {
  id: string;
  path: string;
  isEnabled: boolean;
  lastScannedAt: string | null;
  createdAt: string;
  fileCount: number;
}

/**
 * Request to create a new scan directory.
 * Matches backend: src/Shared/Requests/CreateScanDirectoryRequest.cs
 */
export interface CreateScanDirectoryRequest {
  path: string;
  isEnabled?: boolean;
}

/**
 * Request to update an existing scan directory.
 * Matches backend: src/Shared/Requests/UpdateScanDirectoryRequest.cs
 */
export interface UpdateScanDirectoryRequest {
  path?: string;
  isEnabled?: boolean;
}
