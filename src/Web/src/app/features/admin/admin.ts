import { Component, OnInit, OnDestroy, signal, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReprocessService, ReprocessStats, IndexerConnection } from '../../services/reprocess.service';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatChipsModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatIconModule
  ],
  templateUrl: './admin.html',
  styleUrl: './admin.scss'
})
export class Admin implements OnInit, OnDestroy {
  private reprocessService = inject(ReprocessService);
  private snackBar = inject(MatSnackBar);
  private destroyRef = inject(DestroyRef);

  indexers = signal<IndexerConnection[]>([]);
  stats = signal<ReprocessStats | null>(null);
  processing = signal(false);
  queuedCount = signal(0);
  hasIndexer = signal(false);

  async ngOnInit() {
    await this.reprocessService.connect();
    this.loadStats();

    this.reprocessService.indexers$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(indexers => {
        this.indexers.set(indexers);
        this.hasIndexer.set(indexers.length > 0);
      });
  }

  ngOnDestroy() {
    this.reprocessService.disconnect();
  }

  async loadStats() {
    try {
      const stats = await firstValueFrom(this.reprocessService.getStats());
      this.stats.set(stats);
    } catch (error) {
      console.error('Failed to load stats:', error);
      this.snackBar.open('Failed to load reprocess stats', 'Close', { duration: 5000 });
    }
  }

  async reprocess(filter: 'MissingMetadata' | 'MissingThumbnail' | 'Failed' | 'Heic') {
    this.processing.set(true);
    try {
      const result = await firstValueFrom(this.reprocessService.reprocessByFilter(filter, 500));
      if (result.success) {
        this.queuedCount.set(result.queuedCount);
        this.snackBar.open(`Queued ${result.queuedCount} files for reprocessing`, 'OK', { duration: 5000 });
        // Refresh stats after queuing
        this.loadStats();
      } else {
        this.snackBar.open(`Error: ${result.error}`, 'OK', { duration: 5000 });
        this.processing.set(false);
      }
    } catch (error) {
      console.error('Reprocess error:', error);
      this.snackBar.open('Failed to start reprocessing', 'OK', { duration: 5000 });
      this.processing.set(false);
    }
  }

  getFilterLabel(filter: string): string {
    switch (filter) {
      case 'MissingMetadata': return 'Missing Metadata';
      case 'MissingThumbnail': return 'Missing Thumbnails';
      case 'Failed': return 'Failed Files';
      case 'Heic': return 'HEIC Files';
      default: return filter;
    }
  }
}
