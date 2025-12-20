export interface IndexedFile {
  id: number;
  filePath: string;
  fileName: string;
  fileSize: number;
  sha256Hash: string;
  fileModifiedDate: Date;
  createdAt: Date;
  lastScannedAt: Date;
  width?: number;
  height?: number;
  format?: string;
  colorSpace?: string;
  hasAlpha?: boolean;
  isOriginal?: boolean;
}

export interface DuplicateGroup {
  id: number;
  sha256Hash: string;
  fileCount: number;
  totalSize: number;
  potentialSavings: number;
  files: IndexedFile[];
  originalFileId?: number;
  createdAt: Date;
}

export interface DuplicateGroupSummary {
  id: number;
  sha256Hash: string;
  fileCount: number;
  totalSize: number;
  potentialSavings: number;
  originalFileId?: number;
  sampleFilePath: string;
  createdAt: Date;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface DeleteDuplicatesRequest {
  groupIds: number[];
  keepOriginals: boolean;
  dryRun: boolean;
}

export interface DeleteDuplicatesResponse {
  success: boolean;
  deletedCount: number;
  freedSpace: number;
  errors: string[];
}

export interface SetOriginalRequest {
  groupId: number;
  fileId: number;
}
