import { IndexedFileDto } from './indexed-file.dto';

/**
 * Data transfer object for duplicate file groups.
 * Matches backend: src/Shared/Dtos/DuplicateGroupDto.cs
 */
export interface DuplicateGroupDto {
  id: string;
  hash: string;
  fileCount: number;
  totalSize: number;
  potentialSavings: number;
  resolvedAt: string | null;
  createdAt: string;
  originalFileId: string | null;
  files: IndexedFileDto[];

  // Thumbnail for list display
  firstFileThumbnailPath: string | null;

  // Validation fields
  status: string;
  validatedAt: string | null;
  keptFileId: string | null;

  // Review session fields
  lastReviewedAt: string | null;
  reviewOrder: number | null;
  reviewSessionId: string | null;
}

/**
 * Request to set a file as the original in a duplicate group.
 * Matches backend: src/Shared/Requests/SetOriginalRequest.cs
 */
export interface SetOriginalRequest {
  fileId: string;
}
