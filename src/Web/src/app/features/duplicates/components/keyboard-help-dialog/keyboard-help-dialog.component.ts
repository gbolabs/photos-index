import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-keyboard-help-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Keyboard Shortcuts</h2>
    <mat-dialog-content>
      <table class="shortcuts-table">
        <tbody>
          <tr>
            <td><kbd>&uarr;</kbd> <kbd>&darr;</kbd></td>
            <td>Previous/Next group</td>
          </tr>
          <tr>
            <td><kbd>&larr;</kbd> <kbd>&rarr;</kbd></td>
            <td>Previous/Next file</td>
          </tr>
          <tr>
            <td><kbd>Space</kbd></td>
            <td>Select file as original</td>
          </tr>
          <tr>
            <td><kbd>Enter</kbd></td>
            <td>Apply folder pattern to all matching groups</td>
          </tr>
          <tr>
            <td><kbd>1</kbd> - <kbd>9</kbd></td>
            <td>Quick select file</td>
          </tr>
          <tr>
            <td><kbd>A</kbd></td>
            <td>Auto-select</td>
          </tr>
          <tr>
            <td><kbd>S</kbd></td>
            <td>Skip group</td>
          </tr>
          <tr>
            <td><kbd>U</kbd></td>
            <td>Undo action</td>
          </tr>
          <tr>
            <td><kbd>Esc</kbd></td>
            <td>Back to list</td>
          </tr>
          <tr>
            <td><kbd>?</kbd></td>
            <td>Show this help</td>
          </tr>
        </tbody>
      </table>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .shortcuts-table {
      width: 100%;
      border-collapse: collapse;

      tr {
        border-bottom: 1px solid rgba(0, 0, 0, 0.1);
      }

      td {
        padding: 0.75rem 0.5rem;
      }

      td:first-child {
        white-space: nowrap;
        width: 120px;
      }
    }

    kbd {
      display: inline-block;
      padding: 0.2rem 0.5rem;
      font-family: monospace;
      font-size: 0.875rem;
      background: #f5f5f5;
      border: 1px solid #ddd;
      border-radius: 4px;
      box-shadow: 0 1px 0 rgba(0, 0, 0, 0.1);
      margin-right: 0.25rem;
    }
  `],
})
export class KeyboardHelpDialogComponent {}
