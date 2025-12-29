import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { GalleryGridComponent } from './components/gallery-grid/gallery-grid';
import { FilterBarComponent } from './components/filter-bar/filter-bar';
import { GalleryStateService, GalleryFilters, TileSize } from './services/gallery-state.service';
import { IndexedFileDto } from '../../models';

@Component({
  selector: 'app-gallery',
  standalone: true,
  imports: [
    CommonModule,
    GalleryGridComponent,
    FilterBarComponent
  ],
  templateUrl: './gallery.html',
  styleUrl: './gallery.scss'
})
export class GalleryComponent implements OnInit {
  private readonly stateService = inject(GalleryStateService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly files = this.stateService.files;
  readonly loading = this.stateService.loading;
  readonly loadingMore = this.stateService.loadingMore;
  readonly hasMore = this.stateService.hasMore;
  readonly filters = this.stateService.filters;
  readonly tileSize = this.stateService.tileSize;
  readonly tileSizePx = this.stateService.tileSizePx;

  readonly directories = signal<string[]>([]);

  ngOnInit(): void {
    // Initialize filters from URL query params (preserves filters on back navigation)
    const queryParams = this.route.snapshot.queryParams as Record<string, string>;
    this.stateService.initFromUrl(queryParams);
    this.stateService.loadFiles();
    this.loadDirectories();
  }

  private async loadDirectories(): Promise<void> {
    // TODO: Load directories from API when endpoint is available
    // For now, extract unique directories from loaded files
  }

  onFiltersChange(partialFilters: Partial<GalleryFilters>): void {
    this.stateService.updateFilters(partialFilters);
  }

  onTileSizeChange(size: TileSize): void {
    this.stateService.setTileSize(size);
  }

  onRefresh(): void {
    this.stateService.loadFiles();
  }

  onShowHiddenChange(): void {
    // Reload files when show hidden toggle changes
    this.stateService.loadFiles();
  }

  onLoadMore(): void {
    this.stateService.loadMore();
  }

  onFileClick(file: IndexedFileDto): void {
    // Navigate to file detail view
    this.router.navigate(['/files', file.id]);
  }

  onFileSelect(file: IndexedFileDto): void {
    // TODO: Implement multi-select functionality
    console.log('File selected:', file.fileName);
  }
}
