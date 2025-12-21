import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { IndexedFileDto } from '../../../../models';

interface MetadataRow {
  label: string;
  value: string;
  icon: string;
}

@Component({
  selector: 'app-file-metadata-table',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatTableModule,
    MatIconModule,
  ],
  templateUrl: './file-metadata-table.component.html',
  styleUrl: './file-metadata-table.component.scss',
})
export class FileMetadataTableComponent {
  file = input<IndexedFileDto>();

  displayedColumns = ['icon', 'label', 'value'];

  getMetadataRows(): MetadataRow[] {
    const f = this.file();
    if (!f) return [];

    const rows: MetadataRow[] = [
      {
        label: 'File Name',
        value: f.fileName,
        icon: 'insert_drive_file',
      },
      {
        label: 'Full Path',
        value: f.filePath,
        icon: 'folder',
      },
      {
        label: 'File Size',
        value: this.formatFileSize(f.fileSize),
        icon: 'sd_storage',
      },
      {
        label: 'Dimensions',
        value: this.formatDimensions(f),
        icon: 'photo_size_select_large',
      },
      {
        label: 'File Hash (SHA-256)',
        value: f.fileHash,
        icon: 'fingerprint',
      },
      {
        label: 'Created Date',
        value: this.formatDate(f.createdAt),
        icon: 'calendar_today',
      },
      {
        label: 'Modified Date',
        value: this.formatDate(f.modifiedAt),
        icon: 'edit_calendar',
      },
      {
        label: 'Indexed Date',
        value: this.formatDate(f.indexedAt),
        icon: 'update',
      },
    ];

    if (f.duplicateGroupId) {
      rows.push({
        label: 'Duplicate Group ID',
        value: f.duplicateGroupId,
        icon: 'content_copy',
      });
    }

    return rows;
  }

  private formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  private formatDimensions(file: IndexedFileDto): string {
    if (file.width && file.height) {
      const megapixels = ((file.width * file.height) / 1000000).toFixed(1);
      return `${file.width} x ${file.height} (${megapixels} MP)`;
    }
    return '-';
  }

  private formatDate(dateString: string | null): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleDateString() + ' ' + date.toLocaleTimeString();
  }
}
