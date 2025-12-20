import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DuplicateGroupListComponent } from './components/duplicate-group-list/duplicate-group-list.component';
import { BulkActionsToolbarComponent } from './components/bulk-actions-toolbar/bulk-actions-toolbar.component';
import { DuplicatesService } from '../../shared/services/duplicates.service';

type ViewMode = 'list' | 'detail';

@Component({
  selector: 'app-duplicates',
  standalone: true,
  imports: [
    CommonModule,
    DuplicateGroupListComponent,
    BulkActionsToolbarComponent,
  ],
  templateUrl: './duplicates.html',
  styleUrl: './duplicates.scss',
})
export class Duplicates implements OnInit {
  viewMode = signal<ViewMode>('list');
  
  constructor(private duplicatesService: DuplicatesService) {}
  
  ngOnInit(): void {
    // Load initial data
  }
}
