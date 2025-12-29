import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { HiddenFolder, CreateHiddenFolderRequest, FolderPath } from '../models';
import { ApiErrorHandler } from './api-error-handler';

/**
 * Service for managing hidden folder rules.
 * Provides CRUD operations for hidden folder configurations.
 */
@Injectable({
  providedIn: 'root'
})
export class HiddenFolderService {
  private http = inject(HttpClient);
  private errorHandler = new ApiErrorHandler();
  private apiUrl = `${environment.apiUrl}/api/hidden-folders`;

  /**
   * Gets all hidden folder rules.
   */
  getAll(): Observable<HiddenFolder[]> {
    return this.http
      .get<HiddenFolder[]>(this.apiUrl)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Gets folder paths for autocomplete with optional search filter.
   * Returns folder paths that exist in indexed files.
   */
  getFolderPaths(search?: string): Observable<FolderPath[]> {
    let params = new HttpParams();
    if (search) {
      params = params.set('search', search);
    }

    return this.http
      .get<FolderPath[]>(`${this.apiUrl}/folder-paths`, { params })
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Creates a new hidden folder rule.
   */
  create(request: CreateHiddenFolderRequest): Observable<HiddenFolder> {
    return this.http
      .post<HiddenFolder>(this.apiUrl, request)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Deletes a hidden folder rule.
   */
  delete(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${id}`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Gets the count of hidden files.
   */
  getHiddenFileCount(): Observable<{ count: number }> {
    return this.http
      .get<{ count: number }>(`${this.apiUrl}/hidden-count`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }
}
