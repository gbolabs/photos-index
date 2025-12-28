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

@Injectable({
  providedIn: 'root',
})
export class SignalRService implements OnDestroy {
  private hubConnection: signalR.HubConnection | null = null;

  connected = signal(false);

  // Event subjects for components to subscribe to
  previewReady$ = new Subject<PreviewReadyEvent>();
  previewFailed$ = new Subject<PreviewFailedEvent>();

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

  ngOnDestroy(): void {
    this.hubConnection?.stop();
    this.previewReady$.complete();
    this.previewFailed$.complete();
  }
}
