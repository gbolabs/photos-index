import { Component, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSliderModule } from '@angular/material/slider';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { FormsModule } from '@angular/forms';
import { IndexedFileDto } from '../../../../models';
import { FileSizePipe } from '../../../../shared/pipes/file-size.pipe';

type ComparisonMode = 'side-by-side' | 'overlay' | 'slider';

@Component({
  selector: 'app-image-comparison',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatSliderModule,
    MatButtonToggleModule,
    FormsModule,
    FileSizePipe,
  ],
  templateUrl: './image-comparison.component.html',
  styleUrl: './image-comparison.component.scss',
})
export class ImageComparisonComponent {
  leftFile = input<IndexedFileDto>();
  rightFile = input<IndexedFileDto>();
  getThumbnailUrl = input<(file: IndexedFileDto) => string>();
  getDownloadUrl = input<(file: IndexedFileDto) => string>();

  mode = signal<ComparisonMode>('side-by-side');
  sliderPosition = signal(50);
  overlayOpacity = signal(50);

  getLeftImageUrl(): string {
    const file = this.leftFile();
    const fn = this.getDownloadUrl();
    if (!file || !fn) return '';
    return fn(file);
  }

  getRightImageUrl(): string {
    const file = this.rightFile();
    const fn = this.getDownloadUrl();
    if (!file || !fn) return '';
    return fn(file);
  }

  formatDimensions(file: IndexedFileDto | undefined): string {
    if (!file || !file.width || !file.height) return '-';
    return `${file.width} x ${file.height}`;
  }

  formatDate(dateString: string | null | undefined): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleDateString();
  }

  onSliderChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.sliderPosition.set(Number(input.value));
  }

  onOpacityChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.overlayOpacity.set(Number(input.value));
  }
}
