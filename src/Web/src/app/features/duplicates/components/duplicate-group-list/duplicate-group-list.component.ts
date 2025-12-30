import {
  Component,
  OnInit,
  inject,
  signal,
  output,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { DuplicateService } from '../../../../services/duplicate.service';
import { DuplicateGroupDto, PagedResponse } from '../../../../models';
import { FileSizePipe } from '../../../../shared/pipes/file-size.pipe';

type SortColumn = 'fileCount' | 'totalSize' | 'potentialSavings' | 'status';
type SortDirection = 'asc' | 'desc' | '';

@Component({
  selector: 'app-duplicate-group-list',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatTooltipModule,
    MatCheckboxModule,
    MatSortModule,
    MatSelectModule,
    MatFormFieldModule,
    FileSizePipe,
  ],
  templateUrl: './duplicate-group-list.component.html',
  styleUrl: './duplicate-group-list.component.scss',
})
export class DuplicateGroupListComponent implements OnInit {
  private duplicateService = inject(DuplicateService);

  // State
  loading = signal(true);
  error = signal<string | null>(null);
  groups = signal<DuplicateGroupDto[]>([]);
  totalItems = signal(0);
  selectedGroupIds = signal<Set<string>>(new Set());

  // Pagination
  pageIndex = 0;
  pageSize = 20;

  // Sorting
  sortColumn = signal<SortColumn>('totalSize');
  sortDirection = signal<SortDirection>('desc');

  // Filtering
  statusFilter = signal<string>('');
  statusOptions = [
    { value: '', label: 'All Status' },
    { value: 'pending', label: 'Pending' },
    { value: 'proposed', label: 'Proposed' },
    { value: 'auto-selected', label: 'Auto-selected' },
    { value: 'validated', label: 'Validated' },
  ];

  // Table columns
  displayedColumns = [
    'select',
    'thumbnail',
    'fileCount',
    'totalSize',
    'potentialSavings',
    'status',
    'actions',
  ];

  // Output events
  groupSelected = output<DuplicateGroupDto>();
  selectionChanged = output<string[]>();

  // Computed
  allSelected = computed(() => {
    const selected = this.selectedGroupIds();
    const all = this.groups();
    return all.length > 0 && all.every((g) => selected.has(g.id));
  });

  someSelected = computed(() => {
    const selected = this.selectedGroupIds();
    const all = this.groups();
    return all.some((g) => selected.has(g.id)) && !this.allSelected();
  });

  ngOnInit(): void {
    this.loadGroups();
  }

  loadGroups(): void {
    this.loading.set(true);
    this.error.set(null);

    const status = this.statusFilter() || undefined;
    this.duplicateService.getAll(this.pageIndex + 1, this.pageSize, status).subscribe({
      next: (response: PagedResponse<DuplicateGroupDto>) => {
        // Apply client-side sorting since API doesn't support sort params
        const sortedItems = this.sortGroups(response.items);
        this.groups.set(sortedItems);
        this.totalItems.set(response.totalItems);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load duplicate groups:', err);
        this.error.set('Failed to load duplicate groups');
        this.loading.set(false);
      },
    });
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.selectedGroupIds.set(new Set());
    this.loadGroups();
  }

  getThumbnailUrl(group: DuplicateGroupDto, index: number = 0): string {
    // In list view, files array is empty for performance.
    // Use firstFileThumbnailPath or construct from group hash (all files share the same hash)
    if (group.firstFileThumbnailPath) {
      return `/thumbnails/${group.firstFileThumbnailPath}`;
    }
    // All files in a duplicate group have the same hash, so we can use it for thumbnails
    if (group.hash) {
      return `/thumbnails/thumbs/${group.hash}.jpg`;
    }
    // If files are loaded (e.g., from detail view), use them
    if (group.files && group.files.length > index) {
      const file = group.files[index];
      return this.duplicateService.getThumbnailUrl(file.id, file.thumbnailPath, file.fileHash);
    }
    return 'assets/placeholder.svg';
  }

  hasMultipleFiles(group: DuplicateGroupDto): boolean {
    // Use fileCount since files array is empty in list view
    return group.fileCount >= 2;
  }

  isResolved(group: DuplicateGroupDto): boolean {
    return group.resolvedAt !== null && group.originalFileId !== null;
  }

  viewGroup(group: DuplicateGroupDto): void {
    this.groupSelected.emit(group);
  }

  toggleSelection(group: DuplicateGroupDto): void {
    const current = new Set(this.selectedGroupIds());
    if (current.has(group.id)) {
      current.delete(group.id);
    } else {
      current.add(group.id);
    }
    this.selectedGroupIds.set(current);
    this.selectionChanged.emit(Array.from(current));
  }

  toggleAllSelection(): void {
    if (this.allSelected()) {
      this.selectedGroupIds.set(new Set());
    } else {
      const allIds = new Set(this.groups().map((g) => g.id));
      this.selectedGroupIds.set(allIds);
    }
    this.selectionChanged.emit(Array.from(this.selectedGroupIds()));
  }

  isSelected(group: DuplicateGroupDto): boolean {
    return this.selectedGroupIds().has(group.id);
  }

  autoSelectOriginal(group: DuplicateGroupDto, event: Event): void {
    event.stopPropagation();
    this.duplicateService.autoSelect(group.id).subscribe({
      next: () => {
        this.loadGroups();
      },
      error: (err) => {
        console.error('Failed to auto-select original:', err);
      },
    });
  }

  onSortChange(sort: Sort): void {
    if (!sort.active || !sort.direction) {
      // Reset to default sort
      this.sortColumn.set('totalSize');
      this.sortDirection.set('desc');
    } else {
      this.sortColumn.set(sort.active as SortColumn);
      this.sortDirection.set(sort.direction as SortDirection);
    }

    // Re-sort current data
    const sortedGroups = this.sortGroups(this.groups());
    this.groups.set(sortedGroups);
  }

  onFilterChange(status: string): void {
    this.statusFilter.set(status);
    this.pageIndex = 0; // Reset to first page
    this.selectedGroupIds.set(new Set());
    this.loadGroups();
  }

  private sortGroups(groups: DuplicateGroupDto[]): DuplicateGroupDto[] {
    const column = this.sortColumn();
    const direction = this.sortDirection();

    if (!direction) {
      return groups;
    }

    const multiplier = direction === 'asc' ? 1 : -1;

    return [...groups].sort((a, b) => {
      let comparison = 0;

      switch (column) {
        case 'fileCount':
          comparison = a.fileCount - b.fileCount;
          break;
        case 'totalSize':
          comparison = a.totalSize - b.totalSize;
          break;
        case 'potentialSavings':
          comparison = a.potentialSavings - b.potentialSavings;
          break;
        case 'status':
          comparison = (a.status || '').localeCompare(b.status || '');
          break;
      }

      return comparison * multiplier;
    });
  }

  getStatusLabel(status: string | null): string {
    if (!status) return 'Pending';
    const option = this.statusOptions.find(o => o.value === status);
    return option?.label || status.charAt(0).toUpperCase() + status.slice(1);
  }
}
