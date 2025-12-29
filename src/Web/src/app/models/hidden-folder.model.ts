/**
 * Data transfer object for hidden folder information.
 * Matches backend: src/Shared/Dtos/HiddenFolderDto.cs
 */
export interface HiddenFolder {
  id: string;
  folderPath: string;
  description?: string;
  createdAt: Date;
  affectedFileCount: number;
}

/**
 * Request object for creating a hidden folder rule.
 * Matches backend: src/Shared/Requests/CreateHiddenFolderRequest.cs
 */
export interface CreateHiddenFolderRequest {
  folderPath: string;
  description?: string;
}

/**
 * Folder path information with file count.
 * Used for autocomplete suggestions when creating hidden folder rules.
 */
export interface FolderPath {
  path: string;
  fileCount: number;
}

/**
 * Size-based hiding rule DTO.
 * Matches backend: src/Shared/Dtos/HiddenSizeRuleDto.cs
 */
export interface HiddenSizeRule {
  id: string;
  maxWidth: number;
  maxHeight: number;
  description?: string;
  createdAt: Date;
  affectedFileCount: number;
}

/**
 * Request object for creating a size-based hiding rule.
 */
export interface CreateHiddenSizeRuleRequest {
  maxWidth: number;
  maxHeight: number;
  description?: string;
}

/**
 * Preview result for size rule.
 */
export interface SizeRulePreview {
  totalFiles: number;
  totalSizeBytes: number;
  sizeGroups: SizeGroup[];
}

/**
 * Group of files by dimensions.
 */
export interface SizeGroup {
  width: number;
  height: number;
  fileCount: number;
  totalSizeBytes: number;
}
