import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IndexedFileDto } from '../../../../models';

@Component({
  selector: 'app-gallery-tile',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './gallery-tile.html',
  styleUrl: './gallery-tile.scss'
})
export class GalleryTileComponent {
  readonly file = input.required<IndexedFileDto>();
  readonly thumbnailUrl = input.required<string>();
  readonly size = input<number>(180);
  readonly selected = input(false);

  readonly click = output<IndexedFileDto>();
  readonly select = output<IndexedFileDto>();

  onTileClick(event: MouseEvent): void {
    if (event.ctrlKey || event.metaKey) {
      this.select.emit(this.file());
    } else {
      this.click.emit(this.file());
    }
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.click.emit(this.file());
    }
  }

  get aspectRatio(): string {
    const file = this.file();
    if (file.width && file.height) {
      return `${file.width} / ${file.height}`;
    }
    return '1 / 1';
  }
}
