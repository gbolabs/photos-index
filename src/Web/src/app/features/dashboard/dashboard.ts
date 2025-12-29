import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { ApiService } from '../../core/api.service';
import { FileStatisticsDto, ScanDirectoryDto, SystemVersionsDto } from '../../core/models';
import { FileSizePipe } from '../../shared/pipes/file-size.pipe';

@Component({
  selector: 'app-dashboard',
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    FileSizePipe,
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit {
  private api = inject(ApiService);

  loading = signal(true);
  error = signal<string | null>(null);
  statistics = signal<FileStatisticsDto | null>(null);
  directories = signal<ScanDirectoryDto[]>([]);
  systemVersions = signal<SystemVersionsDto | null>(null);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set(null);

    // Load statistics
    this.api.getStatistics().subscribe({
      next: (stats) => {
        this.statistics.set(stats);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load statistics:', err);
        this.error.set('Failed to load statistics');
        this.loading.set(false);
      },
    });

    // Load directories
    this.api.getDirectories().subscribe({
      next: (dirs) => this.directories.set(dirs),
      error: (err) => console.error('Failed to load directories:', err),
    });

    // Load system versions
    this.api.getSystemVersions().subscribe({
      next: (versions) => this.systemVersions.set(versions),
      error: (err) => console.error('Failed to load system versions:', err),
    });
  }

  formatDate(dateString: string | null): string {
    if (!dateString) return 'Never';
    const date = new Date(dateString);
    return date.toLocaleString();
  }

  getEnabledDirectories(): number {
    return this.directories().filter((d) => d.isEnabled).length;
  }
}
