import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { BehaviorSubject, Observable, firstValueFrom } from 'rxjs';

export interface ReprocessResult {
  success: boolean;
  queuedCount: number;
  error?: string;
}

export interface ReprocessStats {
  missingMetadata: number;
  missingThumbnail: number;
  failed: number;
  heicUnprocessed: number;
  connectedIndexers: number;
}

export interface ReprocessProgress {
  fileId: string;
  status: 'checking' | 'reading' | 'hashing' | 'uploading' | 'complete' | 'failed';
  error?: string;
}

export type IndexerState = 'Idle' | 'Scanning' | 'Processing' | 'Reprocessing' | 'Error' | 'Disconnected';

export interface IndexerStatus {
  indexerId: string;
  hostname: string;
  version?: string;
  commitHash?: string;
  environment?: string;
  state: IndexerState;
  currentDirectory?: string;
  currentActivity?: string;
  filesProcessed: number;
  filesTotal: number;
  errorCount: number;
  lastScanStarted?: string;
  lastScanCompleted?: string;
  connectedAt: string;
  lastHeartbeat: string;
  uptime: string;
  lastError?: string;
}

@Injectable({ providedIn: 'root' })
export class ReprocessService {
  private http = inject(HttpClient);
  private hubConnection?: HubConnection;

  private progressSubject = new BehaviorSubject<Map<string, ReprocessProgress>>(new Map());
  private indexersSubject = new BehaviorSubject<IndexerStatus[]>([]);
  private connectedSubject = new BehaviorSubject<boolean>(false);

  progress$ = this.progressSubject.asObservable();
  indexers$ = this.indexersSubject.asObservable();
  connected$ = this.connectedSubject.asObservable();

  async connect(): Promise<void> {
    if (this.hubConnection?.state === HubConnectionState.Connected) return;

    this.hubConnection = new HubConnectionBuilder()
      .withUrl('/hubs/indexer')
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('ReprocessProgress', (fileId: string, status: string) => {
      const progress = this.progressSubject.value;
      progress.set(fileId, { fileId, status: status as ReprocessProgress['status'] });
      this.progressSubject.next(new Map(progress));
    });

    this.hubConnection.on('ReprocessComplete', (fileId: string, success: boolean, error?: string) => {
      const progress = this.progressSubject.value;
      progress.set(fileId, {
        fileId,
        status: success ? 'complete' : 'failed',
        error
      });
      this.progressSubject.next(new Map(progress));
    });

    this.hubConnection.on('IndexerConnected', (indexerId: string, hostname: string) => {
      // Add a placeholder status for the newly connected indexer
      const existing = this.indexersSubject.value.find(i => i.indexerId === indexerId);
      if (!existing) {
        const newIndexer: IndexerStatus = {
          indexerId,
          hostname,
          state: 'Idle',
          filesProcessed: 0,
          filesTotal: 0,
          errorCount: 0,
          connectedAt: new Date().toISOString(),
          lastHeartbeat: new Date().toISOString(),
          uptime: '00:00:00'
        };
        this.indexersSubject.next([...this.indexersSubject.value, newIndexer]);
      }
    });

    this.hubConnection.on('IndexerDisconnected', (indexerId: string) => {
      const indexers = this.indexersSubject.value.filter(i => i.indexerId !== indexerId);
      this.indexersSubject.next(indexers);
    });

    this.hubConnection.on('IndexerStatusUpdated', (status: IndexerStatus) => {
      const indexers = this.indexersSubject.value;
      const idx = indexers.findIndex(i => i.indexerId === status.indexerId);
      if (idx >= 0) {
        indexers[idx] = status;
        this.indexersSubject.next([...indexers]);
      } else {
        this.indexersSubject.next([...indexers, status]);
      }
    });

    this.hubConnection.onreconnected(() => this.connectedSubject.next(true));
    this.hubConnection.onclose(() => this.connectedSubject.next(false));

    await this.hubConnection.start();
    await this.hubConnection.invoke('JoinUIGroup');
    this.connectedSubject.next(true);

    // Load initial indexers
    const indexers = await firstValueFrom(this.getConnectedIndexers());
    this.indexersSubject.next(indexers);
  }

  disconnect(): void {
    this.hubConnection?.stop();
  }

  // API calls
  getStats(): Observable<ReprocessStats> {
    return this.http.get<ReprocessStats>('/api/reprocess/stats');
  }

  getConnectedIndexers(): Observable<IndexerStatus[]> {
    return this.http.get<IndexerStatus[]>('/api/indexers');
  }

  refreshIndexers(): Observable<void> {
    return this.http.post<void>('/api/indexers/refresh', {});
  }

  reprocessFile(fileId: string): Observable<ReprocessResult> {
    return this.http.post<ReprocessResult>(`/api/reprocess/file/${fileId}`, {});
  }

  reprocessFiles(fileIds: string[]): Observable<ReprocessResult> {
    return this.http.post<ReprocessResult>('/api/reprocess/files', { fileIds });
  }

  reprocessByFilter(
    filter: 'MissingMetadata' | 'MissingThumbnail' | 'Failed' | 'Heic',
    limit?: number
  ): Observable<ReprocessResult> {
    const params = limit ? `?limit=${limit}` : '';
    return this.http.post<ReprocessResult>(`/api/reprocess/filter/${filter}${params}`, {});
  }

  clearProgress(fileId?: string): void {
    if (fileId) {
      const progress = this.progressSubject.value;
      progress.delete(fileId);
      this.progressSubject.next(new Map(progress));
    } else {
      this.progressSubject.next(new Map());
    }
  }
}
