import { Injectable, OnDestroy, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

export interface PreviewReadyEvent {
  fileId: string;
  previewUrl: string;
}

export interface PreviewFailedEvent {
  fileId: string;
  error: string;
}

export interface ScanQueueItem {
  directoryPath: string;
  estimatedFileCount?: number;
  priority: number;
}

export type IndexerState = 'Idle' | 'Scanning' | 'Processing' | 'Reprocessing' | 'Paused' | 'Error' | 'Disconnected';

export interface IndexerStatusEvent {
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
  filesPerSecond: number;
  bytesProcessed: number;
  bytesTotal: number;
  bytesPerSecond: number;
  estimatedSecondsRemaining?: number;
  progressPercentage: number;
  scanQueue?: ScanQueueItem[];
  queuedDirectories: number;
}

export interface ScanProgressEvent {
  directoryPath: string;
  filesProcessed: number;
  filesTotal: number;
}

export interface ScanCompleteEvent {
  directoryPath: string;
  filesScanned: number;
  filesIngested: number;
  filesFailed: number;
}

export interface IndexerConnectedEvent {
  indexerId: string;
  hostname: string;
}

@Injectable({
  providedIn: 'root',
})
export class SignalRService implements OnDestroy {
  private hubConnection: signalR.HubConnection | null = null;

  connected = signal(false);

  // Event subjects for components to subscribe to
  previewReady$ = new Subject<PreviewReadyEvent>();
  previewFailed$ = new Subject<PreviewFailedEvent>();
  indexerConnected$ = new Subject<IndexerConnectedEvent>();
  indexerDisconnected$ = new Subject<string>();
  indexerStatus$ = new Subject<IndexerStatusEvent>();
  scanTriggered$ = new Subject<string | null>();
  scanProgress$ = new Subject<ScanProgressEvent>();
  scanComplete$ = new Subject<ScanCompleteEvent>();
  scanPaused$ = new Subject<void>();
  scanResumed$ = new Subject<void>();
  scanCancelled$ = new Subject<void>();

  constructor() {
    this.connect();
  }

  private connect(): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/indexer')
      .withAutomaticReconnect([1000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Register event handlers
    this.hubConnection.on('PreviewReady', (fileId: string, previewUrl: string) => {
      console.log('Preview ready:', fileId, previewUrl);
      this.previewReady$.next({ fileId, previewUrl });
    });

    this.hubConnection.on('PreviewFailed', (fileId: string, error: string) => {
      console.warn('Preview failed:', fileId, error);
      this.previewFailed$.next({ fileId, error });
    });

    this.hubConnection.on('IndexerConnected', (indexerId: string, hostname: string) => {
      console.log('Indexer connected:', indexerId, hostname);
      this.indexerConnected$.next({ indexerId, hostname });
    });

    this.hubConnection.on('IndexerDisconnected', (indexerId: string) => {
      console.log('Indexer disconnected:', indexerId);
      this.indexerDisconnected$.next(indexerId);
    });

    this.hubConnection.on('IndexerStatusUpdated', (status: IndexerStatusEvent) => {
      this.indexerStatus$.next(status);
    });

    this.hubConnection.on('ScanTriggered', (directoryId: string | null) => {
      console.log('Scan triggered:', directoryId);
      this.scanTriggered$.next(directoryId);
    });

    this.hubConnection.on('ScanProgress', (directoryPath: string, filesProcessed: number, filesTotal: number) => {
      this.scanProgress$.next({ directoryPath, filesProcessed, filesTotal });
    });

    this.hubConnection.on('ScanComplete', (directoryPath: string, filesScanned: number, filesIngested: number, filesFailed: number) => {
      console.log('Scan complete:', directoryPath);
      this.scanComplete$.next({ directoryPath, filesScanned, filesIngested, filesFailed });
    });

    this.hubConnection.on('ScanPaused', () => {
      console.log('Scan paused');
      this.scanPaused$.next();
    });

    this.hubConnection.on('ScanResumed', () => {
      console.log('Scan resumed');
      this.scanResumed$.next();
    });

    this.hubConnection.on('ScanCancelled', () => {
      console.log('Scan cancelled');
      this.scanCancelled$.next();
    });

    this.hubConnection.onreconnecting(() => {
      console.log('SignalR reconnecting...');
      this.connected.set(false);
    });

    this.hubConnection.onreconnected(() => {
      console.log('SignalR reconnected');
      this.connected.set(true);
      this.joinUIGroup();
    });

    this.hubConnection.onclose(() => {
      console.log('SignalR connection closed');
      this.connected.set(false);
    });

    // Start connection
    this.startConnection();
  }

  private async startConnection(): Promise<void> {
    try {
      await this.hubConnection?.start();
      console.log('SignalR connected');
      this.connected.set(true);
      await this.joinUIGroup();
    } catch (err) {
      console.error('SignalR connection error:', err);
      // Retry after delay
      setTimeout(() => this.startConnection(), 5000);
    }
  }

  private async joinUIGroup(): Promise<void> {
    try {
      await this.hubConnection?.invoke('JoinUIGroup');
      console.log('Joined UI group');
    } catch (err) {
      console.error('Failed to join UI group:', err);
    }
  }

  // Control methods for scan operations
  async triggerScan(directoryId?: string): Promise<void> {
    try {
      await this.hubConnection?.invoke('TriggerScan', directoryId ?? null);
    } catch (err) {
      console.error('Failed to trigger scan:', err);
    }
  }

  async pauseScan(): Promise<void> {
    try {
      await this.hubConnection?.invoke('PauseScan');
    } catch (err) {
      console.error('Failed to pause scan:', err);
    }
  }

  async resumeScan(): Promise<void> {
    try {
      await this.hubConnection?.invoke('ResumeScan');
    } catch (err) {
      console.error('Failed to resume scan:', err);
    }
  }

  async cancelScan(): Promise<void> {
    try {
      await this.hubConnection?.invoke('CancelScan');
    } catch (err) {
      console.error('Failed to cancel scan:', err);
    }
  }

  async requestAllStatuses(): Promise<void> {
    try {
      await this.hubConnection?.invoke('RequestAllStatuses');
    } catch (err) {
      console.error('Failed to request statuses:', err);
    }
  }

  ngOnDestroy(): void {
    this.hubConnection?.stop();
    this.previewReady$.complete();
    this.previewFailed$.complete();
    this.indexerConnected$.complete();
    this.indexerDisconnected$.complete();
    this.indexerStatus$.complete();
    this.scanTriggered$.complete();
    this.scanProgress$.complete();
    this.scanComplete$.complete();
    this.scanPaused$.complete();
    this.scanResumed$.complete();
    this.scanCancelled$.complete();
  }
}
