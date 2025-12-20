import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { DuplicateGroupDto, SetOriginalRequest, PagedResponse } from '../models';
import { ApiErrorHandler } from './api-error-handler';

/**
 * Service for managing duplicate file groups.
 * Provides operations for viewing, resolving, and managing duplicates.
 */
@Injectable({
  providedIn: 'root'
})
export class DuplicateService {
  private http = inject(HttpClient);
  private errorHandler = new ApiErrorHandler();
  private apiUrl = `${environment.apiUrl}/api/duplicates`;

  /**
   * Gets all duplicate groups with pagination.
   */
  getAll(page = 1, pageSize = 50, resolved = false): Observable<PagedResponse<DuplicateGroupDto>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString())
      .set('resolved', resolved.toString());

    return this.http
      .get<PagedResponse<DuplicateGroupDto>>(this.apiUrl, { params })
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Gets a specific duplicate group by ID.
   */
  getById(id: string): Observable<DuplicateGroupDto> {
    return this.http
      .get<DuplicateGroupDto>(`${this.apiUrl}/${id}`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Sets a file as the original (keeper) in a duplicate group.
   */
  setOriginal(groupId: string, request: SetOriginalRequest): Observable<DuplicateGroupDto> {
    return this.http
      .post<DuplicateGroupDto>(`${this.apiUrl}/${groupId}/set-original`, request)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Auto-selects the best file as original based on criteria (e.g., highest resolution, oldest date).
   * Backend will use heuristics to determine the best file.
   */
  autoSelect(groupId: string): Observable<DuplicateGroupDto> {
    return this.http
      .post<DuplicateGroupDto>(`${this.apiUrl}/${groupId}/auto-select`, {})
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Marks a duplicate group as resolved.
   */
  resolve(groupId: string): Observable<DuplicateGroupDto> {
    return this.http
      .post<DuplicateGroupDto>(`${this.apiUrl}/${groupId}/resolve`, {})
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Deletes all duplicate files in a group except the original.
   */
  deleteDuplicates(groupId: string): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${groupId}/duplicates`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Gets unresolved duplicate groups only.
   */
  getUnresolved(page = 1, pageSize = 50): Observable<PagedResponse<DuplicateGroupDto>> {
    return this.getAll(page, pageSize, false);
  }

  /**
   * Gets resolved duplicate groups only.
   */
  getResolved(page = 1, pageSize = 50): Observable<PagedResponse<DuplicateGroupDto>> {
    return this.getAll(page, pageSize, true);
  }
}
