import { Component, OnInit, signal, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatBadgeModule } from '@angular/material/badge';
import { DuplicateGroupListComponent } from './components/duplicate-group-list/duplicate-group-list.component';
import { DuplicateGroupDetailComponent } from './components/duplicate-group-detail/duplicate-group-detail.component';
import { BulkActionsToolbarComponent } from './components/bulk-actions-toolbar/bulk-actions-toolbar.component';
import { DuplicateTableViewComponent } from './components/duplicate-table-view/duplicate-table-view.component';
import { ViewModeToggleComponent, DisplayMode } from './components/view-mode-toggle/view-mode-toggle.component';
import { DuplicateGroupDto } from '../../models';
import { HiddenStateService } from '../../services/hidden-state.service';

type ViewMode = 'list' | 'detail';

@Component({
  selector: 'app-duplicates',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatBadgeModule,
    DuplicateGroupListComponent,
    DuplicateGroupDetailComponent,
    BulkActionsToolbarComponent,
    DuplicateTableViewComponent,
    ViewModeToggleComponent,
  ],
  templateUrl: './duplicates.html',
  styleUrl: './duplicates.scss',
})
export class Duplicates implements OnInit {
  private route = inject(ActivatedRoute);
  readonly hiddenStateService = inject(HiddenStateService);

  @ViewChild(DuplicateGroupListComponent) groupList!: DuplicateGroupListComponent;
  @ViewChild(DuplicateTableViewComponent) tableView!: DuplicateTableViewComponent;
  @ViewChild(BulkActionsToolbarComponent) toolbar!: BulkActionsToolbarComponent;

  viewMode = signal<ViewMode>('list');
  displayMode = signal<DisplayMode>('grid');
  selectedGroupId = signal<string | null>(null);
  selectedGroupIds = signal<string[]>([]);

  ngOnInit(): void {
    // Check for groupId query parameter to navigate directly to detail view
    const groupId = this.route.snapshot.queryParamMap.get('groupId');
    if (groupId) {
      this.selectedGroupId.set(groupId);
      this.viewMode.set('detail');
    }
  }

  onGroupSelected(group: DuplicateGroupDto): void {
    this.selectedGroupId.set(group.id);
    this.viewMode.set('detail');
  }

  onBackToList(): void {
    this.selectedGroupId.set(null);
    this.viewMode.set('list');
    // Refresh the list when returning
    if (this.displayMode() === 'grid' && this.groupList) {
      this.groupList.loadGroups();
    } else if (this.displayMode() === 'table' && this.tableView) {
      this.tableView.loadGroups();
    }
  }

  onSelectionChanged(groupIds: string[]): void {
    this.selectedGroupIds.set(groupIds);
  }

  onActionCompleted(): void {
    if (this.displayMode() === 'grid' && this.groupList) {
      this.groupList.loadGroups();
    } else if (this.displayMode() === 'table' && this.tableView) {
      this.tableView.loadGroups();
    }
  }

  onRefreshRequested(): void {
    if (this.displayMode() === 'grid' && this.groupList) {
      this.groupList.loadGroups();
    } else if (this.displayMode() === 'table' && this.tableView) {
      this.tableView.loadGroups();
    }
  }

  onDisplayModeChange(mode: DisplayMode): void {
    this.displayMode.set(mode);
  }

  onShowHiddenToggle(): void {
    this.hiddenStateService.toggleShowHidden();
    // Refresh the lists when show hidden changes
    if (this.displayMode() === 'grid' && this.groupList) {
      this.groupList.loadGroups();
    } else if (this.displayMode() === 'table' && this.tableView) {
      this.tableView.loadGroups();
    }
  }
}
