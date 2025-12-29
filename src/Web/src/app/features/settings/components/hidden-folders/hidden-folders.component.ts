import { Component, inject, signal, OnInit, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HiddenFolderService } from '../../../../services/hidden-folder.service';
import { HiddenStateService } from '../../../../services/hidden-state.service';
import { HiddenFolder, HiddenSizeRule, SizeRulePreview, SizeGroup } from '../../../../models';
import { HideFolderDialogComponent } from '../../../../shared/components/hide-folder-dialog/hide-folder-dialog.component';
import { ConfirmDialogComponent, ConfirmDialogData } from '../confirm-dialog/confirm-dialog.component';
import { FileSizePipe } from '../../../../shared/pipes/file-size.pipe';

@Component({
  selector: 'app-hidden-folders',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTabsModule,
    MatCardModule,
    MatExpansionModule,
    FileSizePipe,
  ],
  templateUrl: './hidden-folders.component.html',
  styleUrl: './hidden-folders.component.scss',
})
export class HiddenFoldersComponent implements OnInit {
  private hiddenFolderService = inject(HiddenFolderService);
  private hiddenStateService = inject(HiddenStateService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private destroyRef = inject(DestroyRef);

  // Folder rules state
  hiddenFolders = signal<HiddenFolder[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  displayedColumns: string[] = ['folderPath', 'description', 'affectedFileCount', 'createdAt', 'actions'];

  // Size rules state
  sizeRules = signal<HiddenSizeRule[]>([]);
  sizeRulesLoading = signal(false);
  sizeRulesError = signal<string | null>(null);
  sizeRulesColumns: string[] = ['dimensions', 'description', 'affectedFileCount', 'createdAt', 'actions'];

  // Size rule creation form
  newSizeWidth = signal(256);
  newSizeHeight = signal(256);
  sizeRuleDescription = signal('');
  sizeRulePreview = signal<SizeRulePreview | null>(null);
  previewLoading = signal(false);
  creatingRule = signal(false);

  // Common icon size presets
  sizePresets = [
    { label: '16x16 (Small icons)', width: 16, height: 16 },
    { label: '32x32 (Medium icons)', width: 32, height: 32 },
    { label: '48x48 (Large icons)', width: 48, height: 48 },
    { label: '64x64 (XL icons)', width: 64, height: 64 },
    { label: '128x128 (Jumbo icons)', width: 128, height: 128 },
    { label: '256x256 (Thumbnails)', width: 256, height: 256 },
  ];

  ngOnInit(): void {
    this.loadHiddenFolders();
    this.loadSizeRules();
    this.loadPreview();
  }

  loadHiddenFolders(): void {
    this.loading.set(true);
    this.error.set(null);

    this.hiddenFolderService.getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (folders) => {
          this.hiddenFolders.set(folders);
          this.loading.set(false);
        },
        error: (err) => {
          console.error('Failed to load hidden folders:', err);
          this.error.set('Failed to load hidden folders');
          this.loading.set(false);
        },
      });
  }

  onAddFolder(): void {
    const dialogRef = this.dialog.open(HideFolderDialogComponent, {
      width: '500px',
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (result) {
          this.hiddenFolderService.create(result)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: () => {
                this.snackBar.open('Hidden folder added successfully', 'Close', { duration: 3000 });
                this.loadHiddenFolders();
                this.hiddenStateService.refreshHiddenFilesCount();
              },
              error: (err) => {
                console.error('Failed to add hidden folder:', err);
                this.snackBar.open('Failed to add hidden folder', 'Close', { duration: 5000 });
              },
            });
        }
      });
  }

  onDeleteFolder(folder: HiddenFolder): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Remove Hidden Folder',
        message: `Are you sure you want to remove "${folder.folderPath}" from hidden folders? This will unhide ${folder.affectedFileCount} files.`,
        confirmText: 'Remove',
        cancelText: 'Cancel',
      } as ConfirmDialogData,
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean) => {
        if (confirmed) {
          this.hiddenFolderService.delete(folder.id)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: () => {
                this.snackBar.open('Hidden folder removed successfully', 'Close', { duration: 3000 });
                this.loadHiddenFolders();
                this.hiddenStateService.refreshHiddenFilesCount();
              },
              error: (err) => {
                console.error('Failed to remove hidden folder:', err);
                this.snackBar.open('Failed to remove hidden folder', 'Close', { duration: 5000 });
              },
            });
        }
      });
  }

  formatDate(date: Date): string {
    if (!date) return '-';
    const d = new Date(date);
    return d.toLocaleDateString();
  }

  // Size Rules Methods

  loadSizeRules(): void {
    this.sizeRulesLoading.set(true);
    this.sizeRulesError.set(null);

    this.hiddenFolderService.getSizeRules()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (rules) => {
          this.sizeRules.set(rules);
          this.sizeRulesLoading.set(false);
        },
        error: (err) => {
          console.error('Failed to load size rules:', err);
          this.sizeRulesError.set('Failed to load size rules');
          this.sizeRulesLoading.set(false);
        },
      });
  }

  loadPreview(): void {
    this.previewLoading.set(true);

    this.hiddenFolderService.previewSizeRule(this.newSizeWidth(), this.newSizeHeight())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (preview) => {
          this.sizeRulePreview.set(preview);
          this.previewLoading.set(false);
        },
        error: (err) => {
          console.error('Failed to load preview:', err);
          this.sizeRulePreview.set(null);
          this.previewLoading.set(false);
        },
      });
  }

  onPresetSelected(preset: { width: number; height: number }): void {
    this.newSizeWidth.set(preset.width);
    this.newSizeHeight.set(preset.height);
    this.sizeRuleDescription.set(`Images ${preset.width}x${preset.height} or smaller`);
    this.loadPreview();
  }

  onSizeChanged(): void {
    // Debounce would be better here, but for simplicity we load immediately
    this.loadPreview();
  }

  createSizeRule(): void {
    if (this.newSizeWidth() <= 0 || this.newSizeHeight() <= 0) {
      this.snackBar.open('Width and height must be positive numbers', 'Close', { duration: 3000 });
      return;
    }

    this.creatingRule.set(true);

    this.hiddenFolderService.createSizeRule({
      maxWidth: this.newSizeWidth(),
      maxHeight: this.newSizeHeight(),
      description: this.sizeRuleDescription() || undefined,
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (rule) => {
          this.snackBar.open(`Size rule created, ${rule.affectedFileCount} files hidden`, 'Close', { duration: 3000 });
          this.creatingRule.set(false);
          this.loadSizeRules();
          this.loadPreview();
          this.hiddenStateService.refreshHiddenFilesCount();
          // Reset form
          this.newSizeWidth.set(256);
          this.newSizeHeight.set(256);
          this.sizeRuleDescription.set('');
        },
        error: (err) => {
          console.error('Failed to create size rule:', err);
          const message = err?.error?.message || 'Failed to create size rule';
          this.snackBar.open(message, 'Close', { duration: 5000 });
          this.creatingRule.set(false);
        },
      });
  }

  onDeleteSizeRule(rule: HiddenSizeRule): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Remove Size Rule',
        message: `Are you sure you want to remove the ${rule.maxWidth}x${rule.maxHeight} size rule? This will unhide ${rule.affectedFileCount} files.`,
        confirmText: 'Remove',
        cancelText: 'Cancel',
      } as ConfirmDialogData,
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean) => {
        if (confirmed) {
          this.hiddenFolderService.deleteSizeRule(rule.id)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: () => {
                this.snackBar.open('Size rule removed, files unhidden', 'Close', { duration: 3000 });
                this.loadSizeRules();
                this.loadPreview();
                this.hiddenStateService.refreshHiddenFilesCount();
              },
              error: (err) => {
                console.error('Failed to remove size rule:', err);
                this.snackBar.open('Failed to remove size rule', 'Close', { duration: 5000 });
              },
            });
        }
      });
  }

  formatSizeGroupLabel(group: SizeGroup): string {
    return `${group.width}x${group.height}`;
  }
}
