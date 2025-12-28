import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { HttpClient } from '@angular/common/http';
import { Subscription, filter, take } from 'rxjs';
import { SignalRService } from '../../../services/signalr.service';

export interface ImagePreviewDialogData {
  fileId: string;
  fileName: string;
  thumbnailUrl: string;
}

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
  error = signal<string | null>(null);
  previewUrl = signal<string | null>(null);

  private subscription = new Subscription();

  ngOnInit(): void {
    // Listen for preview ready event for this specific file
    this.subscription.add(
      this.signalR.previewReady$
        .pipe(
          filter((event) => event.fileId === this.data.fileId),
          take(1)
        )
        .subscribe((event) => {
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
          this.error.set(event.error);
          this.loading.set(false);
        })
    );

    // Request the preview
    this.requestPreview();
  }

  private requestPreview(): void {
    this.http.post(`/api/files/${this.data.fileId}/preview`, {}).subscribe({
      error: (err) => {
        console.error('Failed to request preview:', err);
        if (err.status === 503) {
          this.error.set('No indexer available to generate preview. Please try again later.');
        } else if (err.status === 404) {
          this.error.set('File not found');
        } else {
          this.error.set('Failed to request preview');
        }
        this.loading.set(false);
      },
    });
  }

  close(): void {
    this.dialogRef.close();
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }
}
