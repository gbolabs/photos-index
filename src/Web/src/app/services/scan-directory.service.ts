import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import {
  ScanDirectoryDto,
  CreateScanDirectoryRequest,
  UpdateScanDirectoryRequest
} from '../models';
import { ApiErrorHandler } from './api-error-handler';

/**
 * Service for managing scan directory configurations.
 * Provides CRUD operations for scan directories.
 */
@Injectable({
  providedIn: 'root'
})
export class ScanDirectoryService {
  private http = inject(HttpClient);
  private errorHandler = new ApiErrorHandler();
  private apiUrl = `${environment.apiUrl}/api/scan-directories`;

  /**
   * Gets all scan directories.
   */
  getAll(): Observable<ScanDirectoryDto[]> {
    return this.http
      .get<ScanDirectoryDto[]>(this.apiUrl)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Gets a specific scan directory by ID.
   */
  getById(id: string): Observable<ScanDirectoryDto> {
    return this.http
      .get<ScanDirectoryDto>(`${this.apiUrl}/${id}`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Creates a new scan directory.
   */
  create(request: CreateScanDirectoryRequest): Observable<ScanDirectoryDto> {
    return this.http
      .post<ScanDirectoryDto>(this.apiUrl, request)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Updates an existing scan directory.
   */
  update(id: string, request: UpdateScanDirectoryRequest): Observable<ScanDirectoryDto> {
    return this.http
      .put<ScanDirectoryDto>(`${this.apiUrl}/${id}`, request)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Deletes a scan directory.
   */
  delete(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${id}`)
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }

  /**
   * Triggers a scan for a specific directory.
   */
  triggerScan(id: string): Observable<void> {
    return this.http
      .post<void>(`${this.apiUrl}/${id}/scan`, {})
      .pipe(catchError((error) => this.errorHandler.handleError(error)));
  }
}
