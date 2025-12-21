import {
  Component,
  inject,
  input,
  output,
  signal,
  computed,
  OnChanges,
  SimpleChanges,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { DuplicateService } from '../../../../services/duplicate.service';
import { DuplicateGroupDto, IndexedFileDto } from '../../../../models';
import { FileSizePipe } from '../../../../shared/pipes/file-size.pipe';
import { ImageComparisonComponent } from '../image-comparison/image-comparison.component';
import { FileMetadataTableComponent } from '../file-metadata-table/file-metadata-table.component';

@Component({
  selector: 'app-duplicate-group-detail',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    FileSizePipe,
    ImageComparisonComponent,
    FileMetadataTableComponent,
  ],
  templateUrl: './duplicate-group-detail.component.html',
  styleUrl: './duplicate-group-detail.component.scss',
})
export class DuplicateGroupDetailComponent implements OnChanges {
  private duplicateService = inject(DuplicateService);
  private dialog = inject(MatDialog);

  // Input
  groupId = input<string>();

  // Output
  back = output<void>();
  groupUpdated = output<DuplicateGroupDto>();

  // State
  loading = signal(true);
  error = signal<string | null>(null);
  group = signal<DuplicateGroupDto | null>(null);
  selectedFileId = signal<string | null>(null);
  comparisonMode = signal(false);
  comparisonFiles = signal<IndexedFileDto[]>([]);

  // Computed
  files = computed(() => this.group()?.files || []);
  originalFile = computed(() => {
    const g = this.group();
    if (!g?.originalFileId) return null;
    return g.files.find((f) => f.id === g.originalFileId) || null;
  });
  isResolved = computed(() => {
    const g = this.group();
    return g?.resolvedAt !== null && g?.originalFileId !== null;
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['groupId'] && this.groupId()) {
      this.loadGroup();
    }
  }

  loadGroup(): void {
    const id = this.groupId();
    if (!id) return;

    this.loading.set(true);
    this.error.set(null);

    this.duplicateService.getById(id).subscribe({
      next: (group) => {
        this.group.set(group);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load duplicate group:', err);
        this.error.set('Failed to load duplicate group');
        this.loading.set(false);
      },
    });
  }

  goBack(): void {
    this.back.emit();
  }

  getThumbnailUrl(file: IndexedFileDto): string {
    return this.duplicateService.getThumbnailUrl(file.id);
  }

  getDownloadUrl(file: IndexedFileDto): string {
    return this.duplicateService.getDownloadUrl(file.id);
  }

  selectFile(file: IndexedFileDto): void {
    this.selectedFileId.set(file.id);
  }

  isFileSelected(file: IndexedFileDto): boolean {
    return this.selectedFileId() === file.id;
  }

  isOriginal(file: IndexedFileDto): boolean {
    return this.group()?.originalFileId === file.id;
  }

  setAsOriginal(file: IndexedFileDto): void {
    const g = this.group();
    if (!g) return;

    this.duplicateService.setOriginal(g.id, file.id).subscribe({
      next: () => {
        this.loadGroup();
      },
      error: (err) => {
        console.error('Failed to set original:', err);
      },
    });
  }

  autoSelectOriginal(): void {
    const g = this.group();
    if (!g) return;

    this.duplicateService.autoSelect(g.id).subscribe({
      next: () => {
        this.loadGroup();
      },
      error: (err) => {
        console.error('Failed to auto-select original:', err);
      },
    });
  }

  toggleComparison(file: IndexedFileDto): void {
    const current = this.comparisonFiles();
    const index = current.findIndex((f) => f.id === file.id);

    if (index >= 0) {
      this.comparisonFiles.set(current.filter((f) => f.id !== file.id));
    } else if (current.length < 2) {
      this.comparisonFiles.set([...current, file]);
    }

    this.comparisonMode.set(this.comparisonFiles().length === 2);
  }

  isInComparison(file: IndexedFileDto): boolean {
    return this.comparisonFiles().some((f) => f.id === file.id);
  }

  clearComparison(): void {
    this.comparisonFiles.set([]);
    this.comparisonMode.set(false);
  }

  deleteNonOriginals(): void {
    const g = this.group();
    if (!g || !g.originalFileId) return;

    if (!confirm('Are you sure you want to delete all non-original files in this group?')) {
      return;
    }

    this.duplicateService.deleteNonOriginals(g.id).subscribe({
      next: (result) => {
        console.log(`Queued ${result.filesQueued} files for deletion`);
        this.loadGroup();
      },
      error: (err) => {
        console.error('Failed to delete non-originals:', err);
      },
    });
  }

  openFullscreen(file: IndexedFileDto): void {
    window.open(this.getDownloadUrl(file), '_blank');
  }

  formatDate(dateString: string | null): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleDateString() + ' ' + date.toLocaleTimeString();
  }

  formatDimensions(file: IndexedFileDto): string {
    if (file.width && file.height) {
      return `${file.width} x ${file.height}`;
    }
    return '-';
  }

  getFileName(path: string): string {
    return path.split('/').pop() || path;
  }

  getDirectory(path: string): string {
    const parts = path.split('/');
    parts.pop();
    return parts.join('/') || '/';
  }
}
