import { Component } from '@angular/core';
@Component({
  selector: 'app-bulk-actions-toolbar',
  standalone: true,
  template: '<div>Bulk Actions Toolbar</div>'
})
export class BulkActionsToolbarComponent {}
export interface BulkAction { action: string; groupIds: number[]; }
