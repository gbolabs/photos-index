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
import { ApiService } from '../../core/api.service';
import { ScanDirectoryDto } from '../../core/models';

interface DirectoryStatus extends ScanDirectoryDto {
  scanning: boolean;
}

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
  ],
  templateUrl: './indexing.html',
  styleUrl: './indexing.scss',
})
export class Indexing implements OnInit, OnDestroy {
  private api = inject(ApiService);
  private snackBar = inject(MatSnackBar);
  private refreshInterval: ReturnType<typeof setInterval> | null = null;

  loading = signal(true);
  error = signal<string | null>(null);
  directories = signal<DirectoryStatus[]>([]);
  scanningAll = signal(false);

  ngOnInit(): void {
    this.loadDirectories();
    // Auto-refresh every 10 seconds
    this.refreshInterval = setInterval(() => this.loadDirectories(false), 10000);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
    }
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

  formatDate(dateString: string | null): string {
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
}
