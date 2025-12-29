import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import {
  IndexedFileDto,
  FileQueryParameters,
  FileStatisticsDto,
  PagedResponse
} from '../models';
import { ApiErrorHandler } from './api-error-handler';

/**
 * Service for querying and managing indexed files.
 * Provides methods for searching, filtering, and retrieving file statistics.
 */
@Injectable({
  providedIn: 'root'
})
export class IndexedFileService {
  private http = inject(HttpClient);
  private errorHandler = new ApiErrorHandler();
  private apiUrl = `${environment.apiUrl}/api/files`;

  /**
   * Queries files with pagination and filtering.
   */
  query(params?: FileQueryParameters): Observable<PagedResponse<IndexedFileDto>> {
    const httpParams = this.buildQueryParams(params);

    return this.http
      .get<PagedResponse<IndexedFileDto>>(this.apiUrl, { params: httpParams })
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Gets a specific file by ID.
   */
  getById(id: string): Observable<IndexedFileDto> {
    return this.http
      .get<IndexedFileDto>(`${this.apiUrl}/${id}`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Gets file statistics (total files, duplicates, potential savings).
   */
  getStatistics(): Observable<FileStatisticsDto> {
    return this.http
      .get<FileStatisticsDto>(`${this.apiUrl}/stats`)
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
    return `${this.apiUrl}/${fileId}/thumbnail`;
  }

  /**
   * Gets the direct file URL for viewing.
   */
  getFileUrl(fileId: string): string {
    return `${this.apiUrl}/${fileId}/download`;
  }

  /**
   * Downloads the original file.
   */
  downloadFile(fileId: string): Observable<Blob> {
    return this.http
      .get(`${this.apiUrl}/${fileId}/download`, { responseType: 'blob' })
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Deletes a file from the index (soft delete).
   */
  delete(fileId: string): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${fileId}`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Hides one or more files from the default view.
   */
  hideFiles(fileIds: string[]): Observable<void> {
    return this.http
      .post<void>(`${this.apiUrl}/hide`, { fileIds })
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Unhides one or more files, making them visible in the default view.
   */
  unhideFiles(fileIds: string[]): Observable<void> {
    return this.http
      .post<void>(`${this.apiUrl}/unhide`, { fileIds })
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Builds HTTP query parameters from FileQueryParameters.
   */
  private buildQueryParams(params?: FileQueryParameters): HttpParams {
    let httpParams = new HttpParams();

    if (!params) {
      return httpParams;
    }

    if (params.page !== undefined) {
      httpParams = httpParams.set('page', params.page.toString());
    }

    if (params.pageSize !== undefined) {
      httpParams = httpParams.set('pageSize', params.pageSize.toString());
    }

    if (params.directoryId) {
      httpParams = httpParams.set('directoryId', params.directoryId);
    }

    if (params.hasDuplicates !== undefined) {
      httpParams = httpParams.set('hasDuplicates', params.hasDuplicates.toString());
    }

    if (params.minDate) {
      httpParams = httpParams.set('minDate', params.minDate);
    }

    if (params.maxDate) {
      httpParams = httpParams.set('maxDate', params.maxDate);
    }

    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }

    if (params.sortBy) {
      httpParams = httpParams.set('sortBy', params.sortBy);
    }

    if (params.sortDescending !== undefined) {
      httpParams = httpParams.set('sortDescending', params.sortDescending.toString());
    }

    if (params.includeHidden !== undefined) {
      httpParams = httpParams.set('includeHidden', params.includeHidden.toString());
    }

    return httpParams;
  }
}
