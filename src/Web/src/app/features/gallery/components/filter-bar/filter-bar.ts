import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { GalleryFilters, TileSize } from '../../services/gallery-state.service';

@Component({
  selector: 'app-filter-bar',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatButtonToggleModule
  ],
  templateUrl: './filter-bar.html',
  styleUrl: './filter-bar.scss'
})
export class FilterBarComponent {
  readonly filters = input.required<GalleryFilters>();
  readonly tileSize = input.required<TileSize>();
  readonly directories = input<string[]>([]);

  readonly filtersChange = output<Partial<GalleryFilters>>();
  readonly tileSizeChange = output<TileSize>();
  readonly refresh = output<void>();

  onSearchChange(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.filtersChange.emit({ search: value });
  }

  onDirectoryChange(directory: string): void {
    this.filtersChange.emit({ directory: directory || undefined });
  }

  onDuplicatesOnlyChange(checked: boolean): void {
    this.filtersChange.emit({ duplicatesOnly: checked });
  }

  onTileSizeChange(size: TileSize): void {
    this.tileSizeChange.emit(size);
  }

  onRefresh(): void {
    this.refresh.emit();
  }
}
