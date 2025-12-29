import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { Subscription } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ScanDirectoryDto, IndexingStatusDto } from '../../core/models';
import { SignalRService, IndexerStatusEvent } from '../../services/signalr.service';
import { FileSizePipe } from '../../shared/pipes/file-size.pipe';

interface DirectoryStatus extends ScanDirectoryDto {
  scanning: boolean;
}

const DEFAULT_STATUS: IndexingStatusDto = {
  isRunning: false,
  currentDirectoryId: null,
  currentDirectoryPath: null,
  filesScanned: 0,
  filesIngested: 0,
  filesFailed: 0,
  startedAt: null,
  lastUpdatedAt: null,
};

const DEFAULT_INDEXER_STATUS: IndexerStatusEvent = {
  indexerId: '',
  hostname: '',
  state: 'Disconnected',
  filesProcessed: 0,
  filesTotal: 0,
  errorCount: 0,
  connectedAt: '',
  lastHeartbeat: '',
  uptime: '',
  filesPerSecond: 0,
  bytesProcessed: 0,
  bytesTotal: 0,
  bytesPerSecond: 0,
  progressPercentage: 0,
  queuedDirectories: 0,
};

@Component({
  selector: 'app-indexing',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatSnackBarModule,
    MatTooltipModule,
    MatDividerModule,
    FileSizePipe,
  ],
  templateUrl: './indexing.html',
  styleUrl: './indexing.scss',
})
export class Indexing implements OnInit, OnDestroy {
  private api = inject(ApiService);
  private snackBar = inject(MatSnackBar);
  private signalR = inject(SignalRService);
  private refreshInterval: ReturnType<typeof setInterval> | null = null;
  private statusInterval: ReturnType<typeof setInterval> | null = null;
  private subscriptions = new Subscription();

  loading = signal(true);
  error = signal<string | null>(null);
  directories = signal<DirectoryStatus[]>([]);
  scanningAll = signal(false);
  indexingStatus = signal<IndexingStatusDto>(DEFAULT_STATUS);
  indexerStatus = signal<IndexerStatusEvent>(DEFAULT_INDEXER_STATUS);
  signalRConnected = this.signalR.connected;

  ngOnInit(): void {
    this.loadDirectories();
    this.loadIndexingStatus();
    // Auto-refresh every 10 seconds
    this.refreshInterval = setInterval(() => this.loadDirectories(false), 10000);
    // Poll indexing status every 2 seconds for real-time progress
    this.statusInterval = setInterval(() => this.loadIndexingStatus(), 2000);

    // Subscribe to SignalR events
    this.subscriptions.add(
      this.signalR.indexerStatus$.subscribe(status => {
        this.indexerStatus.set(status);
      })
    );

    this.subscriptions.add(
      this.signalR.scanComplete$.subscribe(() => {
        this.loadDirectories(false);
        this.snackBar.open('Scan completed', 'Close', { duration: 3000 });
      })
    );

    this.subscriptions.add(
      this.signalR.scanPaused$.subscribe(() => {
        this.snackBar.open('Scan paused', 'Close', { duration: 3000 });
      })
    );

    this.subscriptions.add(
      this.signalR.scanResumed$.subscribe(() => {
        this.snackBar.open('Scan resumed', 'Close', { duration: 3000 });
      })
    );

    this.subscriptions.add(
      this.signalR.scanCancelled$.subscribe(() => {
        this.loadDirectories(false);
        this.snackBar.open('Scan cancelled', 'Close', { duration: 3000 });
      })
    );

    // Request initial status
    this.signalR.requestAllStatuses();
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
    }
    if (this.statusInterval) {
      clearInterval(this.statusInterval);
    }
    this.subscriptions.unsubscribe();
  }

  loadIndexingStatus(): void {
    this.api.getIndexingStatus().subscribe({
      next: (status) => {
        this.indexingStatus.set(status);
      },
      error: (err) => {
        console.error('Failed to load indexing status:', err);
        this.indexingStatus.set(DEFAULT_STATUS);
      },
    });
  }

  loadDirectories(showLoading = true): void {
    if (showLoading) {
      this.loading.set(true);
    }
    this.error.set(null);

    this.api.getDirectories().subscribe({
      next: (dirs) => {
        // Preserve scanning state when refreshing
        const currentDirs = this.directories();
        const updated = dirs.map((d) => ({
          ...d,
          scanning: currentDirs.find((cd) => cd.id === d.id)?.scanning || false,
        }));
        this.directories.set(updated);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load directories:', err);
        this.error.set('Failed to load directories');
        this.loading.set(false);
      },
    });
  }

  scanDirectory(directory: DirectoryStatus): void {
    // Update scanning state
    this.directories.update((dirs) =>
      dirs.map((d) => (d.id === directory.id ? { ...d, scanning: true } : d))
    );

    this.api.scanDirectory(directory.id).subscribe({
      next: () => {
        this.snackBar.open(`Scan started for ${directory.path}`, 'Close', { duration: 3000 });
        // Reload after a delay to get updated status
        setTimeout(() => {
          this.loadDirectories(false);
          this.directories.update((dirs) =>
            dirs.map((d) => (d.id === directory.id ? { ...d, scanning: false } : d))
          );
        }, 2000);
      },
      error: (err) => {
        console.error('Failed to start scan:', err);
        this.snackBar.open('Failed to start scan', 'Close', { duration: 5000 });
        this.directories.update((dirs) =>
          dirs.map((d) => (d.id === directory.id ? { ...d, scanning: false } : d))
        );
      },
    });
  }

  scanAll(): void {
    this.scanningAll.set(true);
    this.directories.update((dirs) => dirs.map((d) => ({ ...d, scanning: d.isEnabled })));

    this.api.scanAllDirectories().subscribe({
      next: () => {
        this.snackBar.open('Scan started for all directories', 'Close', { duration: 3000 });
        setTimeout(() => {
          this.loadDirectories(false);
          this.scanningAll.set(false);
          this.directories.update((dirs) => dirs.map((d) => ({ ...d, scanning: false })));
        }, 5000);
      },
      error: (err) => {
        console.error('Failed to start scan:', err);
        this.snackBar.open('Failed to start scan', 'Close', { duration: 5000 });
        this.scanningAll.set(false);
        this.directories.update((dirs) => dirs.map((d) => ({ ...d, scanning: false })));
      },
    });
  }

  formatDate(dateString: string | null | undefined): string {
    if (!dateString) return 'Never';
    const date = new Date(dateString);
    return date.toLocaleString();
  }

  getTimeSince(dateString: string | null): string {
    if (!dateString) return 'Never scanned';
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins} minute${diffMins > 1 ? 's' : ''} ago`;
    if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
    return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
  }

  getEnabledCount(): number {
    return this.directories().filter((d) => d.isEnabled).length;
  }

  getTotalFiles(): number {
    return this.directories().reduce((sum, d) => sum + d.fileCount, 0);
  }

  getElapsedTime(): string {
    const status = this.indexingStatus();
    if (!status.startedAt) return '';

    const start = new Date(status.startedAt);
    const now = new Date();
    const diffMs = now.getTime() - start.getTime();
    const diffSecs = Math.floor(diffMs / 1000);
    const diffMins = Math.floor(diffSecs / 60);
    const diffHours = Math.floor(diffMins / 60);

    if (diffHours > 0) {
      return `${diffHours}h ${diffMins % 60}m ${diffSecs % 60}s`;
    }
    if (diffMins > 0) {
      return `${diffMins}m ${diffSecs % 60}s`;
    }
    return `${diffSecs}s`;
  }

  getProgressPercentage(): number {
    const status = this.indexingStatus();
    const total = status.filesScanned;
    const processed = status.filesIngested + status.filesFailed;
    if (total === 0) return 0;
    return Math.round((processed / total) * 100);
  }

  // Indexer control methods
  pauseScan(): void {
    this.signalR.pauseScan();
  }

  resumeScan(): void {
    this.signalR.resumeScan();
  }

  cancelScan(): void {
    this.signalR.cancelScan();
  }

  isIndexerActive(): boolean {
    const state = this.indexerStatus().state;
    return state === 'Scanning' || state === 'Processing' || state === 'Reprocessing';
  }

  isIndexerPaused(): boolean {
    return this.indexerStatus().state === 'Paused';
  }

  isIndexerConnected(): boolean {
    const state = this.indexerStatus().state;
    return state !== 'Disconnected' && this.indexerStatus().indexerId !== '';
  }

  formatEta(): string {
    const seconds = this.indexerStatus().estimatedSecondsRemaining;
    if (!seconds) return '--';

    const hours = Math.floor(seconds / 3600);
    const mins = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;

    if (hours > 0) {
      return `${hours}h ${mins}m`;
    }
    if (mins > 0) {
      return `${mins}m ${secs}s`;
    }
    return `${secs}s`;
  }

  formatSpeed(): string {
    const fps = this.indexerStatus().filesPerSecond;
    if (fps === 0) return '--';
    return `${fps.toFixed(1)} files/s`;
  }

  formatUptime(): string {
    const uptime = this.indexerStatus().uptime;
    if (!uptime) return '--';

    // Parse TimeSpan format (e.g., "02:15:30" or "1.02:15:30")
    const parts = uptime.split(':');
    if (parts.length >= 2) {
      const hours = parseInt(parts[0], 10);
      const mins = parseInt(parts[1], 10);
      if (hours > 0) {
        return `${hours}h ${mins}m`;
      }
      return `${mins}m`;
    }
    return uptime;
  }

  getStateIcon(): string {
    switch (this.indexerStatus().state) {
      case 'Scanning':
      case 'Processing':
        return 'sync';
      case 'Reprocessing':
        return 'autorenew';
      case 'Paused':
        return 'pause_circle';
      case 'Error':
        return 'error';
      case 'Idle':
        return 'check_circle';
      default:
        return 'cloud_off';
    }
  }

  getStateColor(): string {
    switch (this.indexerStatus().state) {
      case 'Scanning':
      case 'Processing':
      case 'Reprocessing':
        return 'primary';
      case 'Paused':
        return 'warn';
      case 'Error':
        return 'warn';
      case 'Idle':
        return 'accent';
      default:
        return '';
    }
  }
}
