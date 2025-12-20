import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  DuplicateGroup,
  DuplicateGroupSummary,
  PaginatedResponse,
  DeleteDuplicatesRequest,
  DeleteDuplicatesResponse,
  SetOriginalRequest,
} from '../models/duplicate.model';

@Injectable({
  providedIn: 'root',
})
export class DuplicatesService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getDuplicateGroups(
    pageNumber: number = 1,
    pageSize: number = 20
  ): Observable<PaginatedResponse<DuplicateGroupSummary>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    return this.http.get<PaginatedResponse<DuplicateGroupSummary>>(
      `${this.apiUrl}/duplicates/groups`,
      { params }
    );
  }

  getDuplicateGroupById(id: number): Observable<DuplicateGroup> {
    return this.http.get<DuplicateGroup>(`${this.apiUrl}/duplicates/groups/${id}`);
  }

  getThumbnailUrl(fileId: number): string {
    return `${this.apiUrl}/files/${fileId}/thumbnail`;
  }

  getImageUrl(fileId: number): string {
    return `${this.apiUrl}/files/${fileId}`;
  }

  setOriginal(request: SetOriginalRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/duplicates/groups/${request.groupId}/original`, {
      fileId: request.fileId,
    });
  }

  autoSelectOriginal(groupId: number): Observable<void> {
    return this.http.post<void>(
      `${this.apiUrl}/duplicates/groups/${groupId}/auto-select-original`,
      {}
    );
  }

  deleteDuplicates(request: DeleteDuplicatesRequest): Observable<DeleteDuplicatesResponse> {
    return this.http.post<DeleteDuplicatesResponse>(
      `${this.apiUrl}/duplicates/delete`,
      request
    );
  }

  deleteNonOriginalsInGroup(groupId: number, dryRun: boolean = false): Observable<DeleteDuplicatesResponse> {
    return this.http.delete<DeleteDuplicatesResponse>(
      `${this.apiUrl}/duplicates/groups/${groupId}/non-originals`,
      { params: { dryRun: dryRun.toString() } }
    );
  }
}
