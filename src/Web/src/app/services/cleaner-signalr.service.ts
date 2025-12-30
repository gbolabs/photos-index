import { Injectable, OnDestroy, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

export interface DeleteFileResult {
  jobId: string;
  fileId: string;
  success: boolean;
  archivePath?: string;
  error?: string;
  wasDryRun: boolean;
}

export interface CleanerStatusEvent {
  cleanerId: string;
  hostname: string;
  state: 'Idle' | 'Processing' | 'Error';
  dryRunEnabled: boolean;
  connectedAt: string;
  lastHeartbeat: string;
}

@Injectable({
  providedIn: 'root',
})
export class CleanerSignalRService implements OnDestroy {
  private hubConnection: signalR.HubConnection | null = null;

  connected = signal(false);

  // Event subjects for components to subscribe to
  cleanerConnected$ = new Subject<{ cleanerId: string; hostname: string }>();
  cleanerDisconnected$ = new Subject<string>();
  cleanerStatus$ = new Subject<CleanerStatusEvent>();
  deleteProgress$ = new Subject<{ jobId: string; fileId: string; status: string }>();
  deleteComplete$ = new Subject<DeleteFileResult>();
  jobComplete$ = new Subject<{ jobId: string; succeeded: number; failed: number; skipped: number }>();

  constructor() {
    this.connect();
  }

  private connect(): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/cleaner')
      .withAutomaticReconnect([1000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Register event handlers
    this.hubConnection.on('CleanerConnected', (cleanerId: string, hostname: string) => {
      console.log('Cleaner connected:', cleanerId, hostname);
      this.cleanerConnected$.next({ cleanerId, hostname });
    });

    this.hubConnection.on('CleanerDisconnected', (cleanerId: string) => {
      console.log('Cleaner disconnected:', cleanerId);
      this.cleanerDisconnected$.next(cleanerId);
    });

    this.hubConnection.on('CleanerStatusUpdated', (status: CleanerStatusEvent) => {
      this.cleanerStatus$.next(status);
    });

    this.hubConnection.on('DeleteProgress', (jobId: string, fileId: string, status: string) => {
      console.log('Delete progress:', jobId, fileId, status);
      this.deleteProgress$.next({ jobId, fileId, status });
    });

    this.hubConnection.on('DeleteComplete', (result: DeleteFileResult) => {
      console.log('Delete complete:', result);
      this.deleteComplete$.next(result);
    });

    this.hubConnection.on('JobComplete', (jobId: string, succeeded: number, failed: number, skipped: number) => {
      console.log('Job complete:', jobId, succeeded, failed, skipped);
      this.jobComplete$.next({ jobId, succeeded, failed, skipped });
    });

    this.hubConnection.onreconnecting(() => {
      console.log('Cleaner SignalR reconnecting...');
      this.connected.set(false);
    });

    this.hubConnection.onreconnected(() => {
      console.log('Cleaner SignalR reconnected');
      this.connected.set(true);
      this.joinUIGroup();
    });

    this.hubConnection.onclose(() => {
      console.log('Cleaner SignalR connection closed');
      this.connected.set(false);
    });

    // Start connection
    this.startConnection();
  }

  private async startConnection(): Promise<void> {
    try {
      await this.hubConnection?.start();
      console.log('Cleaner SignalR connected');
      this.connected.set(true);
      await this.joinUIGroup();
    } catch (err) {
      console.error('Cleaner SignalR connection error:', err);
      // Retry after delay
      setTimeout(() => this.startConnection(), 5000);
    }
  }

  private async joinUIGroup(): Promise<void> {
    try {
      await this.hubConnection?.invoke('JoinUIGroup');
      console.log('Joined cleaner UI group');
    } catch (err) {
      console.error('Failed to join cleaner UI group:', err);
    }
  }

  ngOnDestroy(): void {
    this.hubConnection?.stop();
    this.cleanerConnected$.complete();
    this.cleanerDisconnected$.complete();
    this.cleanerStatus$.complete();
    this.deleteProgress$.complete();
    this.deleteComplete$.complete();
    this.jobComplete$.complete();
  }
}
