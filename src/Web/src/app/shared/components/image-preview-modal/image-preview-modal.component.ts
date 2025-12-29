import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HttpClient } from '@angular/common/http';
import { Subscription, filter, take, timer } from 'rxjs';
import { SignalRService } from '../../../services/signalr.service';

export interface ImagePreviewDialogData {
  fileId: string;
  fileName: string;
  thumbnailUrl: string;
}

type LoadingState = 'connecting' | 'requesting' | 'waiting' | 'loading-image';

@Component({
  selector: 'app-image-preview-modal',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './image-preview-modal.component.html',
  styleUrl: './image-preview-modal.component.scss',
})
export class ImagePreviewModalComponent implements OnInit, OnDestroy {
  data: ImagePreviewDialogData = inject(MAT_DIALOG_DATA);
  dialogRef = inject(MatDialogRef<ImagePreviewModalComponent>);
  private http = inject(HttpClient);
  private signalR = inject(SignalRService);

  loading = signal(true);
  loadingState = signal<LoadingState>('connecting');
  error = signal<string | null>(null);
  previewUrl = signal<string | null>(null);
  retryCount = signal(0);
  canRetry = signal(false);
  signalRConnected = this.signalR.connected;

  private static readonly TIMEOUT_MS = 30000; // 30 second timeout
  private static readonly MAX_RETRIES = 3;

  private subscription = new Subscription();
  private timeoutId: ReturnType<typeof setTimeout> | null = null;

  get loadingMessage(): string {
    switch (this.loadingState()) {
      case 'connecting':
        return 'Connecting to server...';
      case 'requesting':
        return 'Requesting full resolution...';
      case 'waiting':
        return 'Waiting for indexer to process...';
      case 'loading-image':
        return 'Loading image...';
    }
  }

  ngOnInit(): void {
    // Check SignalR connection first
    if (!this.signalR.connected()) {
      this.loadingState.set('connecting');
      // Wait for connection before proceeding
      this.subscription.add(
        timer(0, 500).pipe(
          filter(() => this.signalR.connected()),
          take(1)
        ).subscribe(() => {
          this.setupSignalRListeners();
          this.requestPreview();
        })
      );
      // Timeout if SignalR doesn't connect within 10 seconds
      this.timeoutId = setTimeout(() => {
        if (!this.signalR.connected()) {
          this.error.set('Unable to establish real-time connection. Please refresh and try again.');
          this.loading.set(false);
          this.canRetry.set(true);
        }
      }, 10000);
    } else {
      this.setupSignalRListeners();
      this.requestPreview();
    }
  }

  private setupSignalRListeners(): void {
    // Listen for preview ready event for this specific file
    this.subscription.add(
      this.signalR.previewReady$
        .pipe(
          filter((event) => event.fileId === this.data.fileId),
          take(1)
        )
        .subscribe((event) => {
          this.clearTimeout();
          this.loadingState.set('loading-image');
          this.previewUrl.set(event.previewUrl);
          this.loading.set(false);
        })
    );

    // Listen for preview failed event
    this.subscription.add(
      this.signalR.previewFailed$
        .pipe(
          filter((event) => event.fileId === this.data.fileId),
          take(1)
        )
        .subscribe((event) => {
          this.clearTimeout();
          this.error.set(event.error);
          this.loading.set(false);
          this.canRetry.set(this.retryCount() < ImagePreviewModalComponent.MAX_RETRIES);
        })
    );
  }

  private requestPreview(): void {
    this.loadingState.set('requesting');
    this.error.set(null);
    this.canRetry.set(false);

    this.http.post(`/api/files/${this.data.fileId}/preview`, {}).subscribe({
      next: () => {
        // Request accepted, now wait for SignalR response
        this.loadingState.set('waiting');
        this.startTimeout();
      },
      error: (err) => {
        console.error('Failed to request preview:', err);
        this.clearTimeout();
        if (err.status === 503) {
          this.error.set('No indexer available to generate preview. Please try again later.');
        } else if (err.status === 404) {
          this.error.set('File not found');
        } else {
          this.error.set('Failed to request preview');
        }
        this.loading.set(false);
        this.canRetry.set(this.retryCount() < ImagePreviewModalComponent.MAX_RETRIES);
      },
    });
  }

  private startTimeout(): void {
    this.clearTimeout();
    this.timeoutId = setTimeout(() => {
      if (this.loading() && !this.previewUrl()) {
        this.error.set('Request timed out. The indexer may be busy processing other files.');
        this.loading.set(false);
        this.canRetry.set(this.retryCount() < ImagePreviewModalComponent.MAX_RETRIES);
      }
    }, ImagePreviewModalComponent.TIMEOUT_MS);
  }

  private clearTimeout(): void {
    if (this.timeoutId) {
      clearTimeout(this.timeoutId);
      this.timeoutId = null;
    }
  }

  retry(): void {
    this.retryCount.update(c => c + 1);
    this.loading.set(true);
    this.error.set(null);
    this.previewUrl.set(null);
    this.canRetry.set(false);

    // Re-setup listeners in case they were completed
    this.setupSignalRListeners();
    this.requestPreview();
  }

  close(): void {
    this.dialogRef.close();
  }

  ngOnDestroy(): void {
    this.clearTimeout();
    this.subscription.unsubscribe();
  }
}
