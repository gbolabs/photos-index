import { Component, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { DuplicateService } from '../../../../services/duplicate.service';
import { FileStatisticsDto } from '../../../../models';
import { FileSizePipe } from '../../../../shared/pipes/file-size.pipe';

@Component({
  selector: 'app-bulk-actions-toolbar',
  standalone: true,
  imports: [
    CommonModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatChipsModule,
    FileSizePipe,
  ],
  templateUrl: './bulk-actions-toolbar.component.html',
  styleUrl: './bulk-actions-toolbar.component.scss',
})
export class BulkActionsToolbarComponent {
  private duplicateService = inject(DuplicateService);

  // Inputs
  selectedGroupIds = input<string[]>([]);

  // Outputs
  actionCompleted = output<void>();
  refreshRequested = output<void>();

  // State
  loading = signal(false);
  stats = signal<FileStatisticsDto | null>(null);

  ngOnInit(): void {
    this.loadStats();
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
}
