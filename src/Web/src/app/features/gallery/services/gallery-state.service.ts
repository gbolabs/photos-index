import { Injectable, computed, inject, signal } from '@angular/core';
import { IndexedFileDto, FileQueryParameters, FileSortBy } from '../../../models';
import { IndexedFileService } from '../../../services/indexed-file.service';
import { firstValueFrom } from 'rxjs';

export interface GalleryFilters {
  directory: string | null;
  search: string | null;
  minDate: string | null;
  maxDate: string | null;
  duplicatesOnly: boolean;
}

export type TileSize = 'small' | 'medium' | 'large';

@Injectable({
  providedIn: 'root'
})
export class GalleryStateService {
  private readonly fileService = inject(IndexedFileService);

  // View state
  readonly tileSize = signal<TileSize>('medium');

  // Data state
  readonly files = signal<IndexedFileDto[]>([]);
  readonly loading = signal(false);
  readonly loadingMore = signal(false);
  readonly error = signal<string | null>(null);
  readonly totalItems = signal(0);
  readonly hasMore = signal(true);

  // Pagination state (internal)
  private currentPage = 1;
  private readonly pageSize = 100; // Larger batches for gallery

  // Filter state
  readonly filters = signal<GalleryFilters>({
    directory: null,
    search: null,
    minDate: null,
    maxDate: null,
    duplicatesOnly: false
  });

  // Computed: tile size in pixels
  readonly tileSizePx = computed(() => {
    const sizes: Record<TileSize, number> = {
      small: 120,
      medium: 180,
      large: 240
    };
    return sizes[this.tileSize()];
  });

  // Computed: group files by date
  readonly filesByDate = computed(() => {
    const files = this.files();
    const groups: Map<string, IndexedFileDto[]> = new Map();

    for (const file of files) {
      const date = file.dateTaken || file.createdAt;
      const dateKey = date ? date.substring(0, 10) : 'Unknown';

      if (!groups.has(dateKey)) {
        groups.set(dateKey, []);
      }
      groups.get(dateKey)!.push(file);
    }

    // Sort by date descending
    return Array.from(groups.entries())
      .sort(([a], [b]) => b.localeCompare(a))
      .map(([date, files]) => ({ date, files }));
  });

  /**
   * Load initial files (resets current data)
   */
  async loadFiles(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.currentPage = 1;
    this.files.set([]);

    try {
      const params = this.buildQueryParams();
      const response = await firstValueFrom(this.fileService.query(params));

      this.files.set(response.items);
      this.totalItems.set(response.totalItems);
      this.hasMore.set(response.hasNextPage);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to load files');
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * Load more files (append to current data)
   */
  async loadMore(): Promise<void> {
    if (this.loadingMore() || !this.hasMore()) {
      return;
    }

    this.loadingMore.set(true);

    try {
      this.currentPage++;
      const params = this.buildQueryParams();
      const response = await firstValueFrom(this.fileService.query(params));

      this.files.update(current => [...current, ...response.items]);
      this.hasMore.set(response.hasNextPage);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Failed to load more files');
      this.currentPage--; // Revert on error
    } finally {
      this.loadingMore.set(false);
    }
  }

  /**
   * Update filters and reload
   */
  updateFilters(filters: Partial<GalleryFilters>): void {
    this.filters.update(current => ({ ...current, ...filters }));
    this.loadFiles();
  }

  /**
   * Set tile size
   */
  setTileSize(size: TileSize): void {
    this.tileSize.set(size);
  }

  /**
   * Get thumbnail URL for a file
   */
  getThumbnailUrl(file: IndexedFileDto): string {
    return this.fileService.getThumbnailUrl(file.id, file.thumbnailPath);
  }

  private buildQueryParams(): FileQueryParameters {
    const filters = this.filters();
    return {
      page: this.currentPage,
      pageSize: this.pageSize,
      directoryId: filters.directory || undefined,
      search: filters.search || undefined,
      minDate: filters.minDate || undefined,
      maxDate: filters.maxDate || undefined,
      hasDuplicates: filters.duplicatesOnly || undefined,
      sortBy: FileSortBy.CreatedAt,
      sortDescending: true
    };
  }
}
