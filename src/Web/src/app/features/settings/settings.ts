import { Component, OnInit, signal, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiService } from '../../core/api.service';
import { ScanDirectoryDto, CreateScanDirectoryRequest, UpdateScanDirectoryRequest } from '../../core/models';
import { DirectoryListComponent } from './components/directory-list/directory-list.component';
import { DirectoryFormDialogComponent, DirectoryFormDialogData } from './components/directory-form-dialog/directory-form-dialog.component';
import { ConfirmDialogComponent, ConfirmDialogData } from './components/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatDialogModule,
    MatTooltipModule,
    DirectoryListComponent,
  ],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export class Settings implements OnInit {
  private apiService = inject(ApiService);
  private snackBar = inject(MatSnackBar);
  private dialog = inject(MatDialog);
  private destroyRef = inject(DestroyRef);

  directories = signal<ScanDirectoryDto[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadDirectories();
  }

  private loadDirectories(): void {
    this.loading.set(true);
    this.error.set(null);

    this.apiService.getDirectories()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (directories) => {
          this.directories.set(directories);
          this.loading.set(false);
        },
        error: (error) => {
          console.error('Error loading directories:', error);
          this.error.set('Failed to load directories');
          this.loading.set(false);
          this.snackBar.open('Failed to load directories', 'Close', { duration: 5000 });
        },
      });
  }

  onAddDirectory(): void {
    const dialogRef = this.dialog.open(DirectoryFormDialogComponent, {
      width: '450px',
      data: { mode: 'create' } as DirectoryFormDialogData,
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result: CreateScanDirectoryRequest | undefined) => {
        if (result) {
          this.apiService.createDirectory(result)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: () => {
                this.snackBar.open('Directory added successfully', 'Close', { duration: 3000 });
                this.loadDirectories();
              },
              error: (error) => {
                console.error('Error adding directory:', error);
                this.snackBar.open('Failed to add directory', 'Close', { duration: 5000 });
              },
            });
        }
      });
  }

  onEditDirectory(directory: ScanDirectoryDto): void {
    const dialogRef = this.dialog.open(DirectoryFormDialogComponent, {
      width: '450px',
      data: { mode: 'edit', directory } as DirectoryFormDialogData,
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result: UpdateScanDirectoryRequest | undefined) => {
        if (result) {
          this.apiService.updateDirectory(directory.id, result)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: () => {
                this.snackBar.open('Directory updated successfully', 'Close', { duration: 3000 });
                this.loadDirectories();
              },
              error: (error) => {
                console.error('Error updating directory:', error);
                this.snackBar.open('Failed to update directory', 'Close', { duration: 5000 });
              },
        });
      }
    });
  }

  onDeleteDirectory(directory: ScanDirectoryDto): void {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Delete Directory',
        message: `Are you sure you want to delete "${directory.path}"? This will not delete any files, only remove the directory from scanning.`,
        confirmText: 'Delete',
        cancelText: 'Cancel',
      } as ConfirmDialogData,
    });

    dialogRef.afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean) => {
        if (confirmed) {
          this.apiService.deleteDirectory(directory.id)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: () => {
                this.snackBar.open('Directory deleted successfully', 'Close', { duration: 3000 });
                this.loadDirectories();
              },
              error: (error) => {
                console.error('Error deleting directory:', error);
                this.snackBar.open('Failed to delete directory', 'Close', { duration: 5000 });
              },
            });
        }
      });
  }

  onToggleDirectory(directory: ScanDirectoryDto): void {
    this.apiService
      .updateDirectory(directory.id, { isEnabled: !directory.isEnabled })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          const status = !directory.isEnabled ? 'enabled' : 'disabled';
          this.snackBar.open(`Directory ${status}`, 'Close', { duration: 2000 });
          this.loadDirectories();
        },
        error: (error) => {
          console.error('Error toggling directory:', error);
          this.snackBar.open('Failed to update directory', 'Close', { duration: 5000 });
        },
      });
  }

  onRefresh(): void {
    this.loadDirectories();
    this.snackBar.open('Directories refreshed', 'Close', { duration: 2000 });
  }
}
