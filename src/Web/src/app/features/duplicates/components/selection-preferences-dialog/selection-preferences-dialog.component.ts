import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSliderModule } from '@angular/material/slider';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import {
  SelectionPreferencesService,
  SelectionPreferenceDto,
  SelectionConfigDto,
} from '../../../../services/selection-preferences.service';

@Component({
  selector: 'app-selection-preferences-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSliderModule,
    MatListModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
    MatDividerModule,
  ],
  templateUrl: './selection-preferences-dialog.component.html',
  styleUrl: './selection-preferences-dialog.component.scss',
})
export class SelectionPreferencesDialogComponent implements OnInit {
  private dialogRef = inject(MatDialogRef<SelectionPreferencesDialogComponent>);
  private preferencesService = inject(SelectionPreferencesService);
  private snackBar = inject(MatSnackBar);

  loading = signal(true);
  saving = signal(false);
  recalculating = signal(false);
  config = signal<SelectionConfigDto | null>(null);
  preferences = signal<SelectionPreferenceDto[]>([]);
  newPathPrefix = signal('');
  newPriority = signal(50);
  hasChanges = signal(false);

  ngOnInit(): void {
    this.loadConfig();
  }

  loadConfig(): void {
    this.loading.set(true);
    this.preferencesService.getConfig().subscribe({
      next: (config) => {
        this.config.set(config);
        this.preferences.set([...config.pathPriorities]);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load config:', err);
        this.loading.set(false);
        this.snackBar.open('Failed to load preferences', 'Close', { duration: 3000 });
      },
    });
  }

  addPreference(): void {
    const pathPrefix = this.newPathPrefix().trim();
    if (!pathPrefix) {
      this.snackBar.open('Path prefix is required', 'Close', { duration: 2000 });
      return;
    }

    // Check for duplicate
    if (this.preferences().some((p) => p.pathPrefix === pathPrefix)) {
      this.snackBar.open('Path prefix already exists', 'Close', { duration: 2000 });
      return;
    }

    const newPref: SelectionPreferenceDto = {
      id: crypto.randomUUID(),
      pathPrefix,
      priority: this.newPriority(),
      sortOrder: this.preferences().length,
    };

    this.preferences.update((prefs) => [...prefs, newPref]);
    this.newPathPrefix.set('');
    this.newPriority.set(50);
    this.hasChanges.set(true);
  }

  removePreference(pref: SelectionPreferenceDto): void {
    this.preferences.update((prefs) => prefs.filter((p) => p.id !== pref.id));
    this.hasChanges.set(true);
  }

  updatePriority(pref: SelectionPreferenceDto, newPriority: number): void {
    this.preferences.update((prefs) =>
      prefs.map((p) => (p.id === pref.id ? { ...p, priority: newPriority } : p))
    );
    this.hasChanges.set(true);
  }

  save(): void {
    this.saving.set(true);
    this.preferencesService.savePreferences(this.preferences()).subscribe({
      next: () => {
        this.saving.set(false);
        this.hasChanges.set(false);
        this.snackBar.open('Preferences saved successfully', 'Close', { duration: 2000 });
      },
      error: (err) => {
        console.error('Failed to save preferences:', err);
        this.saving.set(false);
        this.snackBar.open('Failed to save preferences', 'Close', { duration: 3000 });
      },
    });
  }

  resetToDefaults(): void {
    if (!confirm('Reset all preferences to defaults? This cannot be undone.')) {
      return;
    }

    this.saving.set(true);
    this.preferencesService.resetToDefaults().subscribe({
      next: () => {
        this.saving.set(false);
        this.hasChanges.set(false);
        this.snackBar.open('Preferences reset to defaults', 'Close', { duration: 2000 });
        this.loadConfig();
      },
      error: (err) => {
        console.error('Failed to reset preferences:', err);
        this.saving.set(false);
        this.snackBar.open('Failed to reset preferences', 'Close', { duration: 3000 });
      },
    });
  }

  recalculateAll(): void {
    if (!confirm('Recalculate original selections for all duplicate groups? This may take a while.')) {
      return;
    }

    this.recalculating.set(true);
    this.preferencesService.recalculateOriginals({ scope: 'all' }).subscribe({
      next: (result) => {
        this.recalculating.set(false);
        this.snackBar.open(
          `Recalculated: ${result.updated} updated, ${result.conflicts} conflicts`,
          'Close',
          { duration: 4000 }
        );
      },
      error: (err) => {
        console.error('Failed to recalculate:', err);
        this.recalculating.set(false);
        this.snackBar.open('Failed to recalculate originals', 'Close', { duration: 3000 });
      },
    });
  }

  close(): void {
    if (this.hasChanges() && !confirm('Discard unsaved changes?')) {
      return;
    }
    this.dialogRef.close();
  }

  getPriorityLabel(priority: number): string {
    if (priority >= 80) return 'High';
    if (priority >= 50) return 'Medium';
    if (priority >= 20) return 'Low';
    return 'Very Low';
  }
}
