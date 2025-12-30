import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ScanDirectoryDto } from '../../../../core/models';

@Component({
  selector: 'app-directory-list',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
  ],
  templateUrl: './directory-list.component.html',
  styleUrl: './directory-list.component.scss',
})
export class DirectoryListComponent {
  directories = input.required<ScanDirectoryDto[]>();
  loading = input<boolean>(false);

  edit = output<ScanDirectoryDto>();
  delete = output<ScanDirectoryDto>();
  toggle = output<ScanDirectoryDto>();
  refreshMetadata = output<ScanDirectoryDto>();

  displayedColumns: string[] = ['path', 'status', 'fileCount', 'lastScanned', 'actions'];

  onEdit(directory: ScanDirectoryDto): void {
    this.edit.emit(directory);
  }

  onDelete(directory: ScanDirectoryDto): void {
    this.delete.emit(directory);
  }

  onToggle(directory: ScanDirectoryDto): void {
    this.toggle.emit(directory);
  }

  onRefreshMetadata(directory: ScanDirectoryDto): void {
    this.refreshMetadata.emit(directory);
  }

  formatDate(dateString: string | null): string {
    if (!dateString) {
      return 'Never';
    }
    const date = new Date(dateString);
    return date.toLocaleString();
  }
}
