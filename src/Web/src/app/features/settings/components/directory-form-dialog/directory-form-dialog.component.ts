import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { ScanDirectoryDto } from '../../../../core/models';

export interface DirectoryFormDialogData {
  directory?: ScanDirectoryDto;
  mode: 'create' | 'edit';
}

@Component({
  selector: 'app-directory-form-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatButtonModule,
  ],
  templateUrl: './directory-form-dialog.component.html',
  styleUrl: './directory-form-dialog.component.scss',
})
export class DirectoryFormDialogComponent implements OnInit {
  data: DirectoryFormDialogData = inject(MAT_DIALOG_DATA);
  dialogRef = inject(MatDialogRef<DirectoryFormDialogComponent>);
  private fb = inject(FormBuilder);

  form!: FormGroup;
  loading = signal(false);

  get isEditMode(): boolean {
    return this.data.mode === 'edit';
  }

  get title(): string {
    return this.isEditMode ? 'Edit Directory' : 'Add Directory';
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      path: [
        this.data.directory?.path || '',
        [Validators.required, Validators.pattern(/^\/.*$/)],
      ],
      isEnabled: [this.data.directory?.isEnabled ?? true],
    });
  }

  onSubmit(): void {
    if (this.form.valid) {
      this.dialogRef.close(this.form.value);
    }
  }

  onCancel(): void {
    this.dialogRef.close();
  }
}
