import { Component, input, output, inject, OnInit, OnDestroy, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrollingModule, CdkVirtualScrollViewport } from '@angular/cdk/scrolling';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { IndexedFileDto } from '../../../../models';
import { GalleryTileComponent } from '../gallery-tile/gallery-tile';
import { GalleryStateService } from '../../services/gallery-state.service';

@Component({
  selector: 'app-gallery-grid',
  standalone: true,
  imports: [
    CommonModule,
    ScrollingModule,
    MatProgressSpinnerModule,
    GalleryTileComponent
  ],
  templateUrl: './gallery-grid.html',
  styleUrl: './gallery-grid.scss'
})
export class GalleryGridComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly stateService = inject(GalleryStateService);

  @ViewChild(CdkVirtualScrollViewport) viewport!: CdkVirtualScrollViewport;

  readonly files = input.required<IndexedFileDto[]>();
  readonly loading = input(false);
  readonly loadingMore = input(false);
  readonly hasMore = input(true);
  readonly tileSize = input(180);

  readonly fileClick = output<IndexedFileDto>();
  readonly fileSelect = output<IndexedFileDto>();
  readonly loadMore = output<void>();

  // Calculate items per row based on container width
  itemsPerRow = 5;
  private resizeObserver?: ResizeObserver;
  private containerWidth = 0;

  ngOnInit(): void {
    this.calculateItemsPerRow();
  }

  ngAfterViewInit(): void {
    // Observe container resize to recalculate grid
    this.resizeObserver = new ResizeObserver(entries => {
      for (const entry of entries) {
        this.containerWidth = entry.contentRect.width;
        this.calculateItemsPerRow();
      }
    });

    if (this.viewport) {
      this.resizeObserver.observe(this.viewport.elementRef.nativeElement);
    }
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
  }

  private calculateItemsPerRow(): void {
    const gap = 4;
    const size = this.tileSize();
    if (this.containerWidth > 0) {
      this.itemsPerRow = Math.max(1, Math.floor((this.containerWidth + gap) / (size + gap)));
    }
  }

  // Group files into rows for virtual scroll
  get rows(): IndexedFileDto[][] {
    const files = this.files();
    const rows: IndexedFileDto[][] = [];

    for (let i = 0; i < files.length; i += this.itemsPerRow) {
      rows.push(files.slice(i, i + this.itemsPerRow));
    }

    return rows;
  }

  get rowHeight(): number {
    return this.tileSize() + 4; // tile size + gap
  }

  getThumbnailUrl(file: IndexedFileDto): string {
    return this.stateService.getThumbnailUrl(file);
  }

  onTileClick(file: IndexedFileDto): void {
    this.fileClick.emit(file);
  }

  onTileSelect(file: IndexedFileDto): void {
    this.fileSelect.emit(file);
  }

  onScroll(): void {
    if (!this.viewport || !this.hasMore() || this.loadingMore()) {
      return;
    }

    const end = this.viewport.getRenderedRange().end;
    const total = this.rows.length;

    // Load more when scrolled to 80% of the list
    if (end >= total * 0.8) {
      this.loadMore.emit();
    }
  }

  trackByRow(_index: number, row: IndexedFileDto[]): string {
    return row.map(f => f.id).join('-');
  }

  trackByFile(_index: number, file: IndexedFileDto): string {
    return file.id;
  }
}
