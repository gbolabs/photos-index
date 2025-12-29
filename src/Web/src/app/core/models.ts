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

export interface BuildInfoDto {
  serviceName: string;
  version: string;
  commitHash: string | null;
  branch: string | null;
  buildTime: string | null;
  runtimeVersion: string;
  environment: string | null;
  startTimeUtc: string;
  uptime: string | null;
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

/**
 * Indexing status DTO.
 * Matches backend: src/Shared/Dtos/IndexingStatusDto.cs
 */
export interface IndexingStatusDto {
  isRunning: boolean;
  currentDirectoryId: string | null;
  currentDirectoryPath: string | null;
  filesScanned: number;
  filesIngested: number;
  filesFailed: number;
  startedAt: string | null;
  lastUpdatedAt: string | null;
}

/**
 * Version information for a single service.
 * Matches backend: src/Shared/Dtos/SystemVersionsDto.cs
 */
export interface ServiceVersionDto {
  serviceName: string;
  version: string;
  commitHash: string | null;
  branch: string | null;
  buildTime: string | null;
  isAvailable: boolean;
  uptime: string | null;
  instanceId: string | null;
}

/**
 * Aggregated version information for all services.
 * Matches backend: src/Shared/Dtos/SystemVersionsDto.cs
 */
export interface SystemVersionsDto {
  api: ServiceVersionDto;
  web: ServiceVersionDto | null;
  indexers: ServiceVersionDto[];
  thumbnailService: ServiceVersionDto | null;
  metadataService: ServiceVersionDto | null;
  cleaners: ServiceVersionDto[];
}
