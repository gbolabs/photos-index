import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import {
  ScanDirectoryDto,
  FileStatisticsDto,
  CreateScanDirectoryRequest,
  UpdateScanDirectoryRequest,
  PagedResponse,
  BuildInfoDto,
  IndexingStatusDto,
} from './models';

@Injectable({
  providedIn: 'root',
})
export class ApiService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // Version endpoint
  getVersion(): Observable<BuildInfoDto> {
    return this.http.get<BuildInfoDto>(`${this.apiUrl}/api/version`);
  }

  // Statistics endpoints
  getStatistics(): Observable<FileStatisticsDto> {
    return this.http.get<FileStatisticsDto>(`${this.apiUrl}/api/files/stats`);
  }

  // Scan Directory endpoints
  getDirectories(): Observable<ScanDirectoryDto[]> {
    return this.http.get<PagedResponse<ScanDirectoryDto>>(`${this.apiUrl}/api/scan-directories`)
      .pipe(map(response => response.items));
  }

  getDirectory(id: string): Observable<ScanDirectoryDto> {
    return this.http.get<ScanDirectoryDto>(`${this.apiUrl}/api/scan-directories/${id}`);
  }

  createDirectory(request: CreateScanDirectoryRequest): Observable<ScanDirectoryDto> {
    return this.http.post<ScanDirectoryDto>(`${this.apiUrl}/api/scan-directories`, request);
  }

  updateDirectory(id: string, request: UpdateScanDirectoryRequest): Observable<ScanDirectoryDto> {
    return this.http.put<ScanDirectoryDto>(`${this.apiUrl}/api/scan-directories/${id}`, request);
  }

  deleteDirectory(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/api/scan-directories/${id}`);
  }

  scanDirectory(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/api/scan-directories/${id}/scan`, {});
  }

  scanAllDirectories(): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/api/scan-directories/scan-all`, {});
  }

  // Indexing status endpoints
  getIndexingStatus(): Observable<IndexingStatusDto> {
    return this.http.get<IndexingStatusDto>(`${this.apiUrl}/api/indexing/status`);
  }

  // Generic methods for backward compatibility
  get<T>(endpoint: string): Observable<T> {
    return this.http.get<T>(`${this.apiUrl}/${endpoint}`);
  }

  post<T>(endpoint: string, data: any): Observable<T> {
    return this.http.post<T>(`${this.apiUrl}/${endpoint}`, data);
  }

  put<T>(endpoint: string, data: any): Observable<T> {
    return this.http.put<T>(`${this.apiUrl}/${endpoint}`, data);
  }

  delete<T>(endpoint: string): Observable<T> {
    return this.http.delete<T>(`${this.apiUrl}/${endpoint}`);
  }
}
