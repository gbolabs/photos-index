/**
 * Statistics about indexed files and duplicates.
 * Matches backend: src/Shared/Dtos/FileStatisticsDto.cs
 */
export interface FileStatisticsDto {
  totalFiles: number;
  totalSizeBytes: number;
  duplicateGroups: number;
  duplicateFiles: number;
  potentialSavingsBytes: number;
  lastIndexedAt: string | null;
}
