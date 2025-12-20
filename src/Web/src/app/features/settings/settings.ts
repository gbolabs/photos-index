import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../core/api.service';
import { ScanDirectoryDto } from '../../core/models';
import { DirectoryListComponent } from './components/directory-list/directory-list.component';

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
  directories = signal<ScanDirectoryDto[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  constructor(
    private apiService: ApiService,
    private snackBar: MatSnackBar,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.loadDirectories();
  }

  private loadDirectories(): void {
    this.loading.set(true);
    this.error.set(null);

    this.apiService.getDirectories().subscribe({
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
    // TODO: Implement add directory dialog
    this.snackBar.open('Add directory feature coming soon', 'Close', { duration: 2000 });
  }

  onEditDirectory(directory: ScanDirectoryDto): void {
    // TODO: Implement edit directory dialog
    this.snackBar.open('Edit directory feature coming soon', 'Close', { duration: 2000 });
  }

  onDeleteDirectory(directory: ScanDirectoryDto): void {
    if (confirm(`Are you sure you want to delete "${directory.path}"?`)) {
      this.apiService.deleteDirectory(directory.id).subscribe({
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
  }

  onToggleDirectory(directory: ScanDirectoryDto): void {
    this.apiService
      .updateDirectory(directory.id, { isEnabled: !directory.isEnabled })
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
