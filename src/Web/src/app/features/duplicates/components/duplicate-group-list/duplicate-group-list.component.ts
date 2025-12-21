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
import { DuplicateService } from '../../../../services/duplicate.service';
import { DuplicateGroupDto, PagedResponse } from '../../../../models';
import { FileSizePipe } from '../../../../shared/pipes/file-size.pipe';

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

    this.duplicateService.getAll(this.pageIndex + 1, this.pageSize).subscribe({
      next: (response: PagedResponse<DuplicateGroupDto>) => {
        this.groups.set(response.items);
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
    // Get thumbnail URL for a specific file in the group
    if (group.files && group.files.length > index) {
      return this.duplicateService.getThumbnailUrl(group.files[index].id);
    }
    return 'assets/placeholder.svg';
  }

  hasMultipleFiles(group: DuplicateGroupDto): boolean {
    return group.files && group.files.length >= 2;
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
}
