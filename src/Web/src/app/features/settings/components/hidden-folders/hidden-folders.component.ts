import { Component, inject, signal, OnInit, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HiddenFolderService } from '../../../../services/hidden-folder.service';
import { HiddenStateService } from '../../../../services/hidden-state.service';
import { HiddenFolder } from '../../../../models';
import { HideFolderDialogComponent } from '../../../../shared/components/hide-folder-dialog/hide-folder-dialog.component';
import { ConfirmDialogComponent, ConfirmDialogData } from '../confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-hidden-folders',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatSnackBarModule,
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

  hiddenFolders = signal<HiddenFolder[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  displayedColumns: string[] = ['folderPath', 'description', 'affectedFileCount', 'createdAt', 'actions'];

  ngOnInit(): void {
    this.loadHiddenFolders();
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
}
