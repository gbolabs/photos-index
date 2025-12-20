import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ScanDirectoryDto,
  FileStatisticsDto,
  CreateScanDirectoryRequest,
  UpdateScanDirectoryRequest,
} from './models';

@Injectable({
  providedIn: 'root',
})
export class ApiService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // Statistics endpoints
  getStatistics(): Observable<FileStatisticsDto> {
    return this.http.get<FileStatisticsDto>(`${this.apiUrl}/api/statistics`);
  }

  // Scan Directory endpoints
  getDirectories(): Observable<ScanDirectoryDto[]> {
    return this.http.get<ScanDirectoryDto[]>(`${this.apiUrl}/api/directories`);
  }

  getDirectory(id: string): Observable<ScanDirectoryDto> {
    return this.http.get<ScanDirectoryDto>(`${this.apiUrl}/api/directories/${id}`);
  }

  createDirectory(request: CreateScanDirectoryRequest): Observable<ScanDirectoryDto> {
    return this.http.post<ScanDirectoryDto>(`${this.apiUrl}/api/directories`, request);
  }

  updateDirectory(id: string, request: UpdateScanDirectoryRequest): Observable<ScanDirectoryDto> {
    return this.http.put<ScanDirectoryDto>(`${this.apiUrl}/api/directories/${id}`, request);
  }

  deleteDirectory(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/api/directories/${id}`);
  }

  scanDirectory(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/api/directories/${id}/scan`, {});
  }

  scanAllDirectories(): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/api/directories/scan-all`, {});
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
