import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { DuplicateGroupDto, PagedResponse, FileStatisticsDto } from '../models';
import { ApiErrorHandler } from './api-error-handler';

/**
 * Request for auto-selection rules.
 */
export interface AutoSelectRequest {
  preferLargest?: boolean;
  preferOldest?: boolean;
  preferShortestPath?: boolean;
}

/**
 * Service for managing duplicate file groups.
 * Provides operations for viewing, resolving, and managing duplicates.
 */
@Injectable({
  providedIn: 'root',
})
export class DuplicateService {
  private http = inject(HttpClient);
  private errorHandler = new ApiErrorHandler();
  private apiUrl = `${environment.apiUrl}/api/duplicates`;

  /**
   * Gets all duplicate groups with pagination.
   */
  getAll(
    page = 1,
    pageSize = 20
  ): Observable<PagedResponse<DuplicateGroupDto>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    return this.http
      .get<PagedResponse<DuplicateGroupDto>>(this.apiUrl, { params })
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Gets a specific duplicate group by ID with all its files.
   */
  getById(id: string): Observable<DuplicateGroupDto> {
    return this.http
      .get<DuplicateGroupDto>(`${this.apiUrl}/${id}`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Sets a file as the original (keeper) in a duplicate group.
   */
  setOriginal(groupId: string, fileId: string): Observable<void> {
    return this.http
      .put<void>(`${this.apiUrl}/${groupId}/original`, { fileId })
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Auto-selects the best file as original based on configurable rules.
   */
  autoSelect(
    groupId: string,
    request?: AutoSelectRequest
  ): Observable<{ originalFileId: string }> {
    return this.http
      .post<{ originalFileId: string }>(
        `${this.apiUrl}/${groupId}/auto-select`,
        request || {}
      )
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Auto-selects originals for all unresolved duplicate groups.
   */
  autoSelectAll(
    request?: AutoSelectRequest
  ): Observable<{ groupsProcessed: number }> {
    return this.http
      .post<{ groupsProcessed: number }>(
        `${this.apiUrl}/auto-select-all`,
        request || {}
      )
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Gets duplicate statistics.
   */
  getStatistics(): Observable<FileStatisticsDto> {
    return this.http
      .get<FileStatisticsDto>(`${this.apiUrl}/stats`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Queues non-original files in a group for deletion.
   */
  deleteNonOriginals(groupId: string): Observable<{ filesQueued: number }> {
    return this.http
      .delete<{ filesQueued: number }>(
        `${this.apiUrl}/${groupId}/non-originals`
      )
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Gets the thumbnail URL for a file.
   * Priority:
   * 1. If thumbnailPath exists, use direct MinIO URL via Traefik
   * 2. If fileHash exists, construct MinIO URL (thumbnails are stored as thumbs/{hash}.jpg)
   * 3. Fall back to API endpoint (legacy, usually returns 404)
   */
  getThumbnailUrl(fileId: string, thumbnailPath?: string | null, fileHash?: string | null): string {
    if (thumbnailPath) {
      // Direct access to MinIO via Traefik route
      return `/thumbnails/${thumbnailPath}`;
    }
    if (fileHash) {
      // Construct MinIO URL from file hash - thumbnails are stored as thumbs/{hash}.jpg
      return `/thumbnails/thumbs/${fileHash}.jpg`;
    }
    // Fallback to API endpoint (legacy)
    return `${environment.apiUrl}/api/files/${fileId}/thumbnail`;
  }

  /**
   * Gets the download URL for a file.
   */
  getDownloadUrl(fileId: string): string {
    return `${environment.apiUrl}/api/files/${fileId}/download`;
  }

  /**
   * Queue an async duplicate scan job.
   * Returns job ID immediately. Monitor via SignalR or polling.
   */
  queueScanJob(): Observable<ScanJobResponse> {
    return this.http
      .post<ScanJobResponse>(`${this.apiUrl}/scan`, {})
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Get status of a scan job.
   */
  getScanJobStatus(jobId: string): Observable<DuplicateScanJob> {
    return this.http
      .get<DuplicateScanJob>(`${this.apiUrl}/scan/${jobId}`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Get recent scan jobs.
   */
  getRecentScanJobs(): Observable<DuplicateScanJob[]> {
    return this.http
      .get<DuplicateScanJob[]>(`${this.apiUrl}/scan/jobs`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Synchronous scan (for small collections).
   */
  scanForDuplicatesSync(): Observable<DuplicateScanResult> {
    return this.http
      .post<DuplicateScanResult>(`${this.apiUrl}/scan/sync`, {})
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Gets the directory pattern for a duplicate group.
   * Returns information about which other groups share the same directory pattern.
   */
  getPatternForGroup(groupId: string): Observable<DirectoryPatternDto> {
    return this.http
      .get<DirectoryPatternDto>(`${this.apiUrl}/${groupId}/pattern`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Applies a pattern rule to all duplicate groups matching the directory pattern.
   * Sets the original file to be from the preferred directory.
   * Returns the next unresolved group with a different pattern for navigation.
   */
  applyPatternRule(request: ApplyPatternRuleRequest): Observable<ApplyPatternRuleResultDto> {
    return this.http
      .post<ApplyPatternRuleResultDto>(`${this.apiUrl}/patterns/apply`, request)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Gets navigation info for moving between duplicate groups.
   * Returns previous/next group IDs and position information.
   */
  getNavigation(groupId: string, statusFilter?: string): Observable<GroupNavigationDto> {
    let params = new HttpParams();
    if (statusFilter) {
      params = params.set('status', statusFilter);
    }

    return this.http
      .get<GroupNavigationDto>(`${this.apiUrl}/${groupId}/navigation`, { params })
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  // Session methods for keyboard-driven review

  /**
   * Start or resume a keyboard review session.
   */
  startSession(resumeExisting = true): Observable<SelectionSessionDto> {
    return this.http
      .post<SelectionSessionDto>(`${this.apiUrl}/session/start`, { resumeExisting })
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Get the current active session.
   */
  getCurrentSession(): Observable<SelectionSessionDto | null> {
    return this.http
      .get<SelectionSessionDto>(`${this.apiUrl}/session/current`)
      .pipe(catchError(() => {
        // 204 No Content returns null
        return new Observable<SelectionSessionDto | null>(subscriber => {
          subscriber.next(null);
          subscriber.complete();
        });
      }));
  }

  /**
   * Pause the current session.
   */
  pauseSession(sessionId: string): Observable<SelectionSessionDto> {
    return this.http
      .post<SelectionSessionDto>(`${this.apiUrl}/session/${sessionId}/pause`, {})
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Get session progress.
   */
  getSessionProgress(sessionId: string): Observable<SessionProgressDto> {
    return this.http
      .get<SessionProgressDto>(`${this.apiUrl}/session/${sessionId}/progress`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Propose a file as original (keyboard: Space).
   */
  proposeOriginal(groupId: string, fileId: string): Observable<ReviewActionResultDto> {
    return this.http
      .post<ReviewActionResultDto>(`${this.apiUrl}/${groupId}/propose`, { fileId })
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Validate the current selection (keyboard: Enter).
   */
  validateSelection(groupId: string): Observable<ReviewActionResultDto> {
    return this.http
      .post<ReviewActionResultDto>(`${this.apiUrl}/${groupId}/validate-selection`, {})
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Skip the current group (keyboard: S).
   */
  skipGroup(groupId: string): Observable<ReviewActionResultDto> {
    return this.http
      .post<ReviewActionResultDto>(`${this.apiUrl}/${groupId}/skip`, {})
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Undo the last action (keyboard: U).
   */
  undoAction(groupId: string): Observable<ReviewActionResultDto> {
    return this.http
      .post<ReviewActionResultDto>(`${this.apiUrl}/${groupId}/undo`, {})
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }
}

/**
 * Response from queueing a scan job.
 */
export interface ScanJobResponse {
  jobId: string;
  message: string;
}

/**
 * Duplicate scan job status.
 */
export interface DuplicateScanJob {
  id: string;
  status: 'queued' | 'running' | 'completed' | 'failed';
  queuedAt: string;
  startedAt?: string;
  completedAt?: string;
  durationMs: number;
  errorMessage?: string;
  totalFiles: number;
  totalDuplicateHashes: number;
  processedHashes: number;
  newGroupsCreated: number;
  groupsUpdated: number;
  totalGroups: number;
  totalDuplicateFiles: number;
  potentialSavingsBytes: number;
}

/**
 * Result of a synchronous duplicate scan operation.
 */
export interface DuplicateScanResult {
  totalFilesScanned: number;
  newGroupsCreated: number;
  groupsUpdated: number;
  totalGroups: number;
  totalDuplicateFiles: number;
  potentialSavingsBytes: number;
  scanDurationMs: number;
}

/**
 * Information about a directory pattern shared across duplicate groups.
 */
export interface DirectoryPatternDto {
  directories: string[];
  matchingGroupCount: number;
  groupIds: string[];
  patternHash: string;
  totalPotentialSavings: number;
}

/**
 * Strategy for selecting among multiple files in the preferred directory.
 */
export type PatternTieBreaker = 'earliestDate' | 'shortestPath' | 'largestFile' | 'firstIndexed';

/**
 * Request to apply a selection rule to all duplicate groups matching a directory pattern.
 */
export interface ApplyPatternRuleRequest {
  directories: string[];
  preferredDirectory: string;
  tieBreaker?: PatternTieBreaker;
  preview?: boolean;
}

/**
 * Result of applying a pattern rule to multiple duplicate groups.
 */
export interface ApplyPatternRuleResultDto {
  groupsUpdated: number;
  groupsSkipped: number;
  filesMarkedAsOriginal: number;
  nextUnresolvedGroupId?: string;
  skippedGroupReasons?: string[];
}

/**
 * Navigation information for moving between duplicate groups.
 */
export interface GroupNavigationDto {
  previousGroupId?: string;
  nextGroupId?: string;
  currentPosition: number;
  totalGroups: number;
}

/**
 * Keyboard review session data.
 */
export interface SelectionSessionDto {
  id: string;
  createdAt: string;
  resumedAt?: string;
  completedAt?: string;
  status: string;
  totalGroups: number;
  groupsProposed: number;
  groupsValidated: number;
  groupsSkipped: number;
  currentGroupId?: string;
  lastReviewedGroupId?: string;
  lastActivityAt?: string;
}

/**
 * Session progress information.
 */
export interface SessionProgressDto {
  proposed: number;
  validated: number;
  skipped: number;
  remaining: number;
  progressPercent: number;
  nextGroupId?: string;
}

/**
 * Result of a review action (propose, validate, skip, undo).
 */
export interface ReviewActionResultDto {
  success: boolean;
  nextGroupId?: string;
  message?: string;
}
