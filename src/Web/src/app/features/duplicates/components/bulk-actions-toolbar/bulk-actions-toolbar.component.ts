import { Component, inject, input, output, signal, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { DuplicateService, DuplicateScanJob } from '../../../../services/duplicate.service';
import { FileStatisticsDto } from '../../../../models';
import { FileSizePipe } from '../../../../shared/pipes/file-size.pipe';
import { interval, Subscription, takeWhile } from 'rxjs';

@Component({
  selector: 'app-bulk-actions-toolbar',
  standalone: true,
  imports: [
    CommonModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatProgressBarModule,
    MatTooltipModule,
    MatChipsModule,
    FileSizePipe,
  ],
  templateUrl: './bulk-actions-toolbar.component.html',
  styleUrl: './bulk-actions-toolbar.component.scss',
})
export class BulkActionsToolbarComponent implements OnDestroy {
  private duplicateService = inject(DuplicateService);
  private pollSubscription?: Subscription;

  // Inputs
  selectedGroupIds = input<string[]>([]);

  // Outputs
  actionCompleted = output<void>();
  refreshRequested = output<void>();

  // State
  loading = signal(false);
  stats = signal<FileStatisticsDto | null>(null);
  scanJob = signal<DuplicateScanJob | null>(null);
  scanning = signal(false);

  ngOnInit(): void {
    this.loadStats();
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
  }

  loadStats(): void {
    this.duplicateService.getStatistics().subscribe({
      next: (stats) => {
        this.stats.set(stats);
      },
      error: (err) => {
        console.error('Failed to load statistics:', err);
      },
    });
  }

  get selectionCount(): number {
    return this.selectedGroupIds().length;
  }

  get hasSelection(): boolean {
    return this.selectionCount > 0;
  }

  autoSelectAll(): void {
    if (!confirm('Auto-select originals for all unresolved duplicate groups?')) {
      return;
    }

    this.loading.set(true);
    this.duplicateService.autoSelectAll().subscribe({
      next: (result) => {
        console.log(`Auto-selected originals for ${result.groupsProcessed} groups`);
        this.loading.set(false);
        this.loadStats();
        this.actionCompleted.emit();
      },
      error: (err) => {
        console.error('Failed to auto-select all:', err);
        this.loading.set(false);
      },
    });
  }

  refresh(): void {
    this.loadStats();
    this.refreshRequested.emit();
  }

  startScan(): void {
    if (this.scanning()) return;

    this.scanning.set(true);
    this.scanJob.set(null);

    this.duplicateService.queueScanJob().subscribe({
      next: (response) => {
        console.log('Scan job queued:', response.jobId);
        this.pollJobStatus(response.jobId);
      },
      error: (err) => {
        console.error('Failed to start scan:', err);
        this.scanning.set(false);
      },
    });
  }

  private pollJobStatus(jobId: string): void {
    this.pollSubscription?.unsubscribe();

    this.pollSubscription = interval(2000)
      .pipe(
        takeWhile(() => this.scanning())
      )
      .subscribe(() => {
        this.duplicateService.getScanJobStatus(jobId).subscribe({
          next: (job) => {
            this.scanJob.set(job);

            if (job.status === 'completed' || job.status === 'failed') {
              this.scanning.set(false);
              this.pollSubscription?.unsubscribe();
              this.loadStats();
              this.actionCompleted.emit();
            }
          },
          error: (err) => {
            console.error('Failed to get job status:', err);
          },
        });
      });
  }

  get scanProgress(): number {
    const job = this.scanJob();
    if (!job || job.totalDuplicateHashes === 0) return 0;
    return Math.round((job.processedHashes / job.totalDuplicateHashes) * 100);
  }
}
