import {
  Component,
  OnInit,
  inject,
  signal,
  output,
  computed,
  input,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { DuplicateService } from '../../../../services/duplicate.service';
import { DuplicateGroupDto, PagedResponse, IndexedFileDto } from '../../../../models';
import { FileSizePipe } from '../../../../shared/pipes/file-size.pipe';
import { environment } from '../../../../../environments/environment';

type SortColumn = 'size' | 'date' | 'fileCount';
type SortDirection = 'asc' | 'desc';

interface RowState {
  expanded: boolean;
}

@Component({
  selector: 'app-duplicate-table-view',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatCheckboxModule,
    MatTooltipModule,
    MatSortModule,
    MatCardModule,
    MatFormFieldModule,
    MatSelectModule,
    MatChipsModule,
    FileSizePipe,
  ],
  templateUrl: './duplicate-table-view.component.html',
  styleUrl: './duplicate-table-view.component.scss',
})
export class DuplicateTableViewComponent implements OnInit {
  private duplicateService = inject(DuplicateService);

  // State
  loading = signal(true);
  error = signal<string | null>(null);
  groups = signal<DuplicateGroupDto[]>([]);
  totalItems = signal(0);
  selectedGroupIds = signal<Set<string>>(new Set());
  expandedRows = signal<Map<string, RowState>>(new Map());
  groupDetails = signal<Map<string, DuplicateGroupDto>>(new Map());
  loadingDetails = signal<Set<string>>(new Set());

  // Pagination
  pageIndex = 0;
  pageSize = 50;
  pageSizeOptions = [50, 100, 500];

  // Sorting
  sortColumn = signal<SortColumn>('size');
  sortDirection = signal<SortDirection>('desc');

  // Filtering
  statusFilter = signal<string>('');
  statusOptions = [
    { value: '', label: 'All' },
    { value: 'Pending', label: 'Pending' },
    { value: 'AutoSelected', label: 'Auto-selected' },
    { value: 'Validated', label: 'Validated' },
    { value: 'Cleaning', label: 'Cleaning' },
    { value: 'CleaningFailed', label: 'Failed' },
    { value: 'Cleaned', label: 'Cleaned' },
  ];

  // Table columns
  displayedColumns = ['select', 'thumbnail', 'original', 'size', 'date', 'status', 'duplicates'];

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
        // Sort the groups based on current sort settings
        const sortedGroups = this.sortGroups(response.items);
        this.groups.set(sortedGroups);
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

  onFilterChange(status: string): void {
    this.statusFilter.set(status);
    this.pageIndex = 0; // Reset to first page
    this.selectedGroupIds.set(new Set());
    this.loadGroups();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.selectedGroupIds.set(new Set());
    this.loadGroups();
  }

  onSortChange(sort: Sort): void {
    if (!sort.active || !sort.direction) {
      return;
    }

    this.sortColumn.set(sort.active as SortColumn);
    this.sortDirection.set(sort.direction as SortDirection);

    // Re-sort current data
    const sortedGroups = this.sortGroups(this.groups());
    this.groups.set(sortedGroups);
  }

  private sortGroups(groups: DuplicateGroupDto[]): DuplicateGroupDto[] {
    const column = this.sortColumn();
    const direction = this.sortDirection();
    const multiplier = direction === 'asc' ? 1 : -1;

    return [...groups].sort((a, b) => {
      let comparison = 0;

      switch (column) {
        case 'size':
          comparison = a.totalSize - b.totalSize;
          break;
        case 'date':
          comparison = new Date(this.getLatestDate(a)).getTime() - new Date(this.getLatestDate(b)).getTime();
          break;
        case 'fileCount':
          comparison = a.fileCount - b.fileCount;
          break;
      }

      return comparison * multiplier;
    });
  }

  hasOriginalSelected(group: DuplicateGroupDto): boolean {
    return !!group.originalFileId;
  }

  getOriginalFile(group: DuplicateGroupDto): IndexedFileDto | null {
    // Try to get from detailed group data first
    const detailed = this.groupDetails().get(group.id);
    const groupToUse = detailed || group;

    if (groupToUse.originalFileId && groupToUse.files && groupToUse.files.length > 0) {
      return groupToUse.files.find(f => f.id === groupToUse.originalFileId) || null;
    }
    return null;
  }

  getDuplicateFiles(group: DuplicateGroupDto): IndexedFileDto[] {
    // Try to get from detailed group data first
    const detailed = this.groupDetails().get(group.id);
    const groupToUse = detailed || group;

    if (!groupToUse.files || groupToUse.files.length === 0) return [];
    const originalId = groupToUse.originalFileId;
    if (!originalId) return groupToUse.files;
    return groupToUse.files.filter(f => f.id !== originalId);
  }

  isLoadingDetails(groupId: string): boolean {
    return this.loadingDetails().has(groupId);
  }

  hasLoadedDetails(groupId: string): boolean {
    return this.groupDetails().has(groupId);
  }

  getLatestDate(group: DuplicateGroupDto): string {
    if (!group.files || group.files.length === 0) {
      return group.createdAt;
    }
    const dates = group.files.map(f => new Date(f.modifiedAt).getTime());
    return new Date(Math.max(...dates)).toISOString();
  }

  getRowStatusClass(group: DuplicateGroupDto): string {
    if (!group.originalFileId) {
      return 'status-conflict'; // Orange - needs manual selection
    }
    if (group.resolvedAt) {
      return 'status-validated'; // Purple - validated
    }
    return 'status-auto-selected'; // Green - auto-selected
  }

  isRowExpanded(groupId: string): boolean {
    return this.expandedRows().get(groupId)?.expanded || false;
  }

  toggleRowExpansion(groupId: string): void {
    const current = new Map(this.expandedRows());
    const rowState = current.get(groupId) || { expanded: false };
    const newExpandedState = !rowState.expanded;
    rowState.expanded = newExpandedState;
    current.set(groupId, rowState);
    this.expandedRows.set(current);

    // Fetch group details when expanding if not already loaded
    if (newExpandedState && !this.groupDetails().has(groupId)) {
      this.loadGroupDetails(groupId);
    }
  }

  private loadGroupDetails(groupId: string): void {
    // Mark as loading
    const loading = new Set(this.loadingDetails());
    loading.add(groupId);
    this.loadingDetails.set(loading);

    this.duplicateService.getById(groupId).subscribe({
      next: (group) => {
        // Store the detailed group data
        const details = new Map(this.groupDetails());
        details.set(groupId, group);
        this.groupDetails.set(details);

        // Remove from loading
        const loadingSet = new Set(this.loadingDetails());
        loadingSet.delete(groupId);
        this.loadingDetails.set(loadingSet);
      },
      error: (err) => {
        console.error('Failed to load group details:', err);
        // Remove from loading
        const loadingSet = new Set(this.loadingDetails());
        loadingSet.delete(groupId);
        this.loadingDetails.set(loadingSet);
      },
    });
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

  getTruncatedPath(filePath: string, maxLength: number = 40): string {
    if (filePath.length <= maxLength) return filePath;
    const fileName = filePath.split('/').pop() || '';
    const pathPart = filePath.substring(0, filePath.length - fileName.length);
    if (pathPart.length + fileName.length <= maxLength) return filePath;

    const availableLength = maxLength - fileName.length - 3; // 3 for '...'
    if (availableLength <= 0) return `...${fileName}`;

    return `${pathPart.substring(0, availableLength)}...${fileName}`;
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  }

  isExpandedRow = (index: number, group: DuplicateGroupDto) => {
    return this.isRowExpanded(group.id);
  };

  getThumbnailUrl(group: DuplicateGroupDto): string | null {
    if (group.firstFileThumbnailPath) {
      return `${environment.apiUrl}${group.firstFileThumbnailPath}`;
    }
    return null;
  }

  getStatusLabel(status: string): string {
    const option = this.statusOptions.find(o => o.value === status);
    return option?.label || status;
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Pending': return 'status-pending';
      case 'AutoSelected': return 'status-auto-selected';
      case 'Validated': return 'status-validated';
      case 'Cleaning': return 'status-cleaning';
      case 'CleaningFailed': return 'status-failed';
      case 'Cleaned': return 'status-cleaned';
      default: return '';
    }
  }
}
