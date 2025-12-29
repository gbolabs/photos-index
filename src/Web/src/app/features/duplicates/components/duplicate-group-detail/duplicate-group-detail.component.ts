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
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DuplicateService, DirectoryPatternDto, GroupNavigationDto } from '../../../../services/duplicate.service';
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
    MatSnackBarModule,
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
  private snackBar = inject(MatSnackBar);

  // Input
  groupId = input<string>();

  // Output
  back = output<void>();
  groupUpdated = output<DuplicateGroupDto>();
  navigateToGroup = output<string>();

  // State
  loading = signal(true);
  error = signal<string | null>(null);
  group = signal<DuplicateGroupDto | null>(null);
  selectedFileId = signal<string | null>(null);
  comparisonMode = signal(false);
  comparisonFiles = signal<IndexedFileDto[]>([]);

  // Pattern-related state
  patternInfo = signal<DirectoryPatternDto | null>(null);
  applyingPattern = signal(false);

  // Navigation state
  navigation = signal<GroupNavigationDto | null>(null);

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

  /** Unique directories from the files in this group */
  uniqueDirectories = computed(() => {
    const fileList = this.files();
    const dirs = new Set<string>();
    for (const file of fileList) {
      dirs.add(this.getDirectory(file.filePath));
    }
    return Array.from(dirs).sort();
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['groupId'] && this.groupId()) {
      this.loadGroup();
      this.loadPatternInfo();
      this.loadNavigation();
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

  loadPatternInfo(): void {
    const id = this.groupId();
    if (!id) return;

    this.patternInfo.set(null);

    this.duplicateService.getPatternForGroup(id).subscribe({
      next: (pattern) => {
        this.patternInfo.set(pattern);
      },
      error: (err) => {
        console.error('Failed to load pattern info:', err);
        // Pattern info is optional, don't show error
      },
    });
  }

  /**
   * Apply a pattern rule to all groups with the same directory pattern.
   * Selects files from the preferred directory as original.
   */
  applyPatternRule(preferredDirectory: string): void {
    const pattern = this.patternInfo();
    if (!pattern) return;

    this.applyingPattern.set(true);

    this.duplicateService.applyPatternRule({
      directories: pattern.directories,
      preferredDirectory,
      tieBreaker: 'earliestDate',
    }).subscribe({
      next: (result) => {
        this.applyingPattern.set(false);

        const message = `Updated ${result.groupsUpdated} group${result.groupsUpdated !== 1 ? 's' : ''}`;
        this.snackBar.open(message, 'OK', { duration: 3000 });

        if (result.nextUnresolvedGroupId) {
          // Navigate to next group with different pattern
          this.navigateToGroup.emit(result.nextUnresolvedGroupId);
        } else {
          // All groups resolved, go back to list
          this.snackBar.open('All duplicate groups resolved!', 'OK', { duration: 5000 });
          this.back.emit();
        }
      },
      error: (err) => {
        this.applyingPattern.set(false);
        console.error('Failed to apply pattern rule:', err);
        this.snackBar.open('Failed to apply pattern rule', 'Dismiss', { duration: 5000 });
      },
    });
  }

  loadNavigation(): void {
    const id = this.groupId();
    if (!id) return;

    this.navigation.set(null);

    this.duplicateService.getNavigation(id).subscribe({
      next: (nav) => {
        this.navigation.set(nav);
      },
      error: (err) => {
        console.error('Failed to load navigation:', err);
        // Navigation is optional, don't show error
      },
    });
  }

  goToPrevious(): void {
    const nav = this.navigation();
    if (nav?.previousGroupId) {
      this.navigateToGroup.emit(nav.previousGroupId);
    }
  }

  goToNext(): void {
    const nav = this.navigation();
    if (nav?.nextGroupId) {
      this.navigateToGroup.emit(nav.nextGroupId);
    }
  }

  goBack(): void {
    this.back.emit();
  }

  getThumbnailUrl(file: IndexedFileDto): string {
    return this.duplicateService.getThumbnailUrl(file.id, file.thumbnailPath, file.fileHash);
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
