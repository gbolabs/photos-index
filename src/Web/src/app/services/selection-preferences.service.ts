import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { ApiErrorHandler } from './api-error-handler';

/**
 * Selection preference for path-based priority.
 */
export interface SelectionPreferenceDto {
  id: string;
  pathPrefix: string;
  priority: number;
  sortOrder: number;
}

/**
 * Full selection configuration.
 */
export interface SelectionConfigDto {
  pathPriorities: SelectionPreferenceDto[];
  preferExifData: boolean;
  preferDeeperPaths: boolean;
  preferOlderFiles: boolean;
  conflictThreshold: number;
}

/**
 * Request to save preferences.
 */
export interface SavePreferencesRequest {
  preferences: SelectionPreferenceDto[];
}

/**
 * Request to recalculate originals.
 */
export interface RecalculateOriginalsRequest {
  scope?: 'all' | 'pending';
  preview?: boolean;
}

/**
 * Response from recalculate operation.
 */
export interface RecalculateOriginalsResponse {
  updated: number;
  conflicts: number;
  preview?: unknown[];
}

/**
 * Service for managing selection preferences.
 */
@Injectable({
  providedIn: 'root',
})
export class SelectionPreferencesService {
  private http = inject(HttpClient);
  private errorHandler = new ApiErrorHandler();
  private apiUrl = `${environment.apiUrl}/api/selection-preferences`;

  /**
   * Get the current selection configuration.
   */
  getConfig(): Observable<SelectionConfigDto> {
    return this.http
      .get<SelectionConfigDto>(`${this.apiUrl}/config`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Get all path priority preferences.
   */
  getPreferences(): Observable<SelectionPreferenceDto[]> {
    return this.http
      .get<SelectionPreferenceDto[]>(this.apiUrl)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Save path priority preferences.
   */
  savePreferences(preferences: SelectionPreferenceDto[]): Observable<void> {
    return this.http
      .post<void>(this.apiUrl, { preferences })
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Reset preferences to default values.
   */
  resetToDefaults(): Observable<void> {
    return this.http
      .post<void>(`${this.apiUrl}/reset`, {})
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Recalculate original file selections.
   */
  recalculateOriginals(
    request?: RecalculateOriginalsRequest
  ): Observable<RecalculateOriginalsResponse> {
    return this.http
      .post<RecalculateOriginalsResponse>(
        `${this.apiUrl}/recalculate`,
        request || {}
      )
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Get score for a specific file.
   */
  getFileScore(fileId: string): Observable<{ fileId: string; score: number }> {
    return this.http
      .get<{ fileId: string; score: number }>(`${this.apiUrl}/score/${fileId}`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }
}
