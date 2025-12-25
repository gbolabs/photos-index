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
   * If the file has a thumbnailPath (stored in MinIO), returns direct MinIO URL via Traefik.
   * Otherwise falls back to the API endpoint for backward compatibility.
   */
  getThumbnailUrl(fileId: string, thumbnailPath?: string | null): string {
    if (thumbnailPath) {
      // Direct access to MinIO via Traefik route
      return `/thumbnails/${thumbnailPath}`;
    }
    // Fallback to API endpoint
    return `${environment.apiUrl}/api/files/${fileId}/thumbnail`;
  }

  /**
   * Gets the download URL for a file.
   */
  getDownloadUrl(fileId: string): string {
    return `${environment.apiUrl}/api/files/${fileId}/download`;
  }
}
