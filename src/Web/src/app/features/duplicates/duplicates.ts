import { Component, OnInit, signal, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { DuplicateGroupListComponent } from './components/duplicate-group-list/duplicate-group-list.component';
import { DuplicateGroupDetailComponent } from './components/duplicate-group-detail/duplicate-group-detail.component';
import { BulkActionsToolbarComponent } from './components/bulk-actions-toolbar/bulk-actions-toolbar.component';
import { DuplicateGroupDto } from '../../models';

type ViewMode = 'list' | 'detail';

@Component({
  selector: 'app-duplicates',
  standalone: true,
  imports: [
    CommonModule,
    DuplicateGroupListComponent,
    DuplicateGroupDetailComponent,
    BulkActionsToolbarComponent,
  ],
  templateUrl: './duplicates.html',
  styleUrl: './duplicates.scss',
})
export class Duplicates implements OnInit {
  private route = inject(ActivatedRoute);

  @ViewChild(DuplicateGroupListComponent) groupList!: DuplicateGroupListComponent;
  @ViewChild(BulkActionsToolbarComponent) toolbar!: BulkActionsToolbarComponent;

  viewMode = signal<ViewMode>('list');
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
    if (this.groupList) {
      this.groupList.loadGroups();
    }
  }

  onSelectionChanged(groupIds: string[]): void {
    this.selectedGroupIds.set(groupIds);
  }

  onActionCompleted(): void {
    if (this.groupList) {
      this.groupList.loadGroups();
    }
  }

  onRefreshRequested(): void {
    if (this.groupList) {
      this.groupList.loadGroups();
    }
  }
}
