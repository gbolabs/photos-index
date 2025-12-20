/**
 * Data transfer object for indexed file information.
 * Matches backend: src/Shared/Dtos/IndexedFileDto.cs
 */
export interface IndexedFileDto {
  id: string;
  filePath: string;
  fileName: string;
  fileHash: string;
  fileSize: number;
  width: number | null;
  height: number | null;
  createdAt: string;
  modifiedAt: string;
  indexedAt: string;
  thumbnailPath: string | null;
  isDuplicate: boolean;
  duplicateGroupId: string | null;
}

/**
 * Query parameters for filtering and paginating indexed files.
 * Matches backend: src/Shared/Requests/FileQueryParameters.cs
 */
export interface FileQueryParameters {
  page?: number;
  pageSize?: number;
  directoryId?: string;
  hasDuplicates?: boolean;
  minDate?: string;
  maxDate?: string;
  search?: string;
  sortBy?: FileSortBy;
  sortDescending?: boolean;
}

/**
 * File sorting options.
 * Matches backend: src/Shared/Requests/FileSortBy enum
 */
export enum FileSortBy {
  Name = 'Name',
  Size = 'Size',
  CreatedAt = 'CreatedAt',
  ModifiedAt = 'ModifiedAt',
  IndexedAt = 'IndexedAt'
}
