import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { IndexedFileService } from '../../services/indexed-file.service';
import { IndexedFileDto, FileQueryParameters, PagedResponse, FileSortBy } from '../../models';
import { FileSizePipe } from '../../shared/pipes/file-size.pipe';

@Component({
  selector: 'app-files',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatChipsModule,
    MatTooltipModule,
    FileSizePipe,
  ],
  templateUrl: './files.html',
  styleUrl: './files.scss',
})
export class Files implements OnInit {
  private fileService = inject(IndexedFileService);

  loading = signal(true);
  error = signal<string | null>(null);
  files = signal<IndexedFileDto[]>([]);
  totalItems = signal(0);

  // Pagination
  pageIndex = 0;
  pageSize = 25;

  // Filters
  searchQuery = '';
  sortBy: FileSortBy = FileSortBy.CreatedAt;
  sortDescending = true;

  displayedColumns = ['thumbnail', 'fileName', 'size', 'dimensions', 'createdAt', 'actions'];

  ngOnInit(): void {
    this.loadFiles();
  }

  loadFiles(): void {
    this.loading.set(true);
    this.error.set(null);

    const query: FileQueryParameters = {
      page: this.pageIndex + 1,
      pageSize: this.pageSize,
      sortBy: this.sortBy,
      sortDescending: this.sortDescending,
    };

    if (this.searchQuery) {
      query.search = this.searchQuery;
    }

    this.fileService.query(query).subscribe({
      next: (response: PagedResponse<IndexedFileDto>) => {
        this.files.set(response.items);
        this.totalItems.set(response.totalItems);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load files:', err);
        this.error.set('Failed to load files');
        this.loading.set(false);
      },
    });
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadFiles();
  }

  onSearch(): void {
    this.pageIndex = 0;
    this.loadFiles();
  }

  onSortChange(): void {
    this.pageIndex = 0;
    this.loadFiles();
  }

  clearSearch(): void {
    this.searchQuery = '';
    this.onSearch();
  }

  getThumbnailUrl(file: IndexedFileDto): string {
    return this.fileService.getThumbnailUrl(file.id, file.thumbnailPath);
  }

  getFileName(path: string): string {
    return path.split('/').pop() || path;
  }

  formatDate(dateString: string | null): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleDateString();
  }

  formatDimensions(file: IndexedFileDto): string {
    if (file.width && file.height) {
      return `${file.width} x ${file.height}`;
    }
    return '-';
  }

  viewFile(file: IndexedFileDto): void {
    window.open(this.fileService.getFileUrl(file.id), '_blank');
  }
}
