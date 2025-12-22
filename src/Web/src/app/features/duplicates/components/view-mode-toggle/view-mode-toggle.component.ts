import { Component, input, output } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

export type DisplayMode = 'grid' | 'table';

@Component({
  selector: 'app-view-mode-toggle',
  standalone: true,
  imports: [MatButtonToggleModule, MatIconModule, MatTooltipModule],
  template: `
    <mat-button-toggle-group
      [value]="displayMode()"
      (change)="onDisplayModeChange($event.value)"
      aria-label="Display Mode"
      class="view-mode-toggle"
    >
      <mat-button-toggle value="grid" matTooltip="Grid View">
        <mat-icon>grid_view</mat-icon>
      </mat-button-toggle>
      <mat-button-toggle value="table" matTooltip="Table View">
        <mat-icon>table_rows</mat-icon>
      </mat-button-toggle>
    </mat-button-toggle-group>
  `,
  styles: [`
    .view-mode-toggle {
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.12);
    }
  `],
})
export class ViewModeToggleComponent {
  displayMode = input.required<DisplayMode>();
  displayModeChange = output<DisplayMode>();

  onDisplayModeChange(mode: DisplayMode): void {
    this.displayModeChange.emit(mode);
  }
}
