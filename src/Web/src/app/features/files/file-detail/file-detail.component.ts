import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { IndexedFileService } from '../../../services/indexed-file.service';
import { NotificationService } from '../../../services/notification.service';
import { IndexedFileDto } from '../../../models';
import { FileSizePipe } from '../../../shared/pipes/file-size.pipe';

@Component({
  selector: 'app-file-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    MatChipsModule,
    MatTooltipModule,
    FileSizePipe,
  ],
  templateUrl: './file-detail.component.html',
  styleUrl: './file-detail.scss',
})
export class FileDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private fileService = inject(IndexedFileService);
  private notificationService = inject(NotificationService);
  private location = inject(Location);

  loading = signal(true);
  error = signal<string | null>(null);
  file = signal<IndexedFileDto | null>(null);

  ngOnInit(): void {
    const fileId = this.route.snapshot.paramMap.get('id');
    if (fileId) {
      this.loadFile(fileId);
    } else {
      this.error.set('File ID not provided');
      this.loading.set(false);
    }
  }

  loadFile(id: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.fileService.getById(id).subscribe({
      next: (file: IndexedFileDto) => {
        this.file.set(file);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load file:', err);
        this.error.set('Failed to load file details');
        this.loading.set(false);
      },
    });
  }

  getThumbnailUrl(): string {
    const fileData = this.file();
    return fileData ? this.fileService.getThumbnailUrl(fileData.id) : '';
  }

  async copyPath(): Promise<void> {
    const fileData = this.file();
    if (!fileData) return;

    try {
      await navigator.clipboard.writeText(fileData.filePath);
      this.notificationService.success('File path copied to clipboard');
    } catch (err) {
      console.error('Failed to copy path:', err);
      this.notificationService.error('Failed to copy path to clipboard');
    }
  }

  goBack(): void {
    this.location.back();
  }

  formatDate(dateString: string | null): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleString();
  }

  formatDimensions(): string {
    const fileData = this.file();
    if (fileData?.width && fileData?.height) {
      return `${fileData.width} x ${fileData.height}`;
    }
    return '-';
  }

  hasGpsData(): boolean {
    const fileData = this.file();
    return !!(fileData?.gpsLatitude && fileData?.gpsLongitude);
  }

  getGpsCoordinates(): string {
    const fileData = this.file();
    if (this.hasGpsData() && fileData) {
      return `${fileData.gpsLatitude?.toFixed(6)}, ${fileData.gpsLongitude?.toFixed(6)}`;
    }
    return '-';
  }

  getGoogleMapsUrl(): string {
    const fileData = this.file();
    if (this.hasGpsData() && fileData) {
      return `https://www.google.com/maps/search/?api=1&query=${fileData.gpsLatitude},${fileData.gpsLongitude}`;
    }
    return '';
  }

  hasExifData(): boolean {
    const fileData = this.file();
    return !!(
      fileData?.dateTaken ||
      fileData?.cameraMake ||
      fileData?.cameraModel ||
      fileData?.iso ||
      fileData?.aperture ||
      fileData?.shutterSpeed ||
      this.hasGpsData()
    );
  }

  viewFile(): void {
    const fileData = this.file();
    if (fileData) {
      window.open(this.fileService.getFileUrl(fileData.id), '_blank');
    }
  }
}
