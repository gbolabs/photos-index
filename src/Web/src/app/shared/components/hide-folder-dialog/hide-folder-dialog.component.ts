import { Component, inject, signal, OnInit, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged, switchMap, of } from 'rxjs';
import { HiddenFolderService } from '../../../services/hidden-folder.service';
import { FolderPath, CreateHiddenFolderRequest } from '../../../models';

@Component({
  selector: 'app-hide-folder-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './hide-folder-dialog.component.html',
  styleUrl: './hide-folder-dialog.component.scss',
})
export class HideFolderDialogComponent implements OnInit {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<HideFolderDialogComponent>);
  private hiddenFolderService = inject(HiddenFolderService);
  private destroyRef = inject(DestroyRef);

  form!: FormGroup;
  folderSuggestions = signal<FolderPath[]>([]);
  loading = signal(false);
  selectedFolder = signal<FolderPath | null>(null);

  ngOnInit(): void {
    this.form = this.fb.group({
      folderPath: ['', [Validators.required, Validators.pattern(/^\/.*$/)]],
      description: [''],
    });

    // Setup autocomplete
    this.form.get('folderPath')!.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((value) => {
          if (!value || value.length < 2) {
            return of([]);
          }
          this.loading.set(true);
          return this.hiddenFolderService.getFolderPaths(value);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (suggestions) => {
          this.folderSuggestions.set(suggestions);
          this.loading.set(false);
        },
        error: () => {
          this.folderSuggestions.set([]);
          this.loading.set(false);
        },
      });
  }

  onFolderSelected(folder: FolderPath): void {
    this.selectedFolder.set(folder);
    this.form.get('folderPath')!.setValue(folder.path);
  }

  displayFolderPath(folder: FolderPath | string): string {
    if (typeof folder === 'string') {
      return folder;
    }
    return folder?.path || '';
  }

  get previewCount(): number {
    const selected = this.selectedFolder();
    if (selected) {
      return selected.fileCount;
    }
    // Check if current path matches any suggestion
    const currentPath = this.form.get('folderPath')?.value;
    const match = this.folderSuggestions().find(f => f.path === currentPath);
    return match?.fileCount || 0;
  }

  onSubmit(): void {
    if (this.form.valid) {
      const request: CreateHiddenFolderRequest = {
        folderPath: this.form.get('folderPath')!.value,
        description: this.form.get('description')?.value || undefined,
      };
      this.dialogRef.close(request);
    }
  }

  onCancel(): void {
    this.dialogRef.close();
  }
}
