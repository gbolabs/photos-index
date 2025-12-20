import { Injectable, inject } from '@angular/core';
import { MatSnackBar, MatSnackBarConfig } from '@angular/material/snack-bar';

/**
 * Service for displaying toast/snackbar notifications to users.
 * Uses Angular Material's MatSnackBar for consistent UI.
 */
@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private snackBar = inject(MatSnackBar);

  private defaultConfig: MatSnackBarConfig = {
    duration: 3000,
    horizontalPosition: 'end',
    verticalPosition: 'bottom'
  };

  /**
   * Shows a success notification with green styling.
   */
  success(message: string, duration = 3000): void {
    this.snackBar.open(message, 'Close', {
      ...this.defaultConfig,
      duration,
      panelClass: ['notification-success']
    });
  }

  /**
   * Shows an error notification with red styling.
   * @param message The error message to display
   * @param duration How long to show the notification (default 5000ms)
   * @param traceId Optional trace ID for debugging - will be appended to message
   */
  error(message: string, duration = 5000, traceId?: string): void {
    const displayMessage = traceId
      ? `${message} (Trace: ${traceId.substring(0, 8)}...)`
      : message;

    this.snackBar.open(displayMessage, 'Close', {
      ...this.defaultConfig,
      duration,
      panelClass: ['notification-error']
    });
  }

  /**
   * Shows an info notification with blue styling.
   */
  info(message: string, duration = 3000): void {
    this.snackBar.open(message, 'Close', {
      ...this.defaultConfig,
      duration,
      panelClass: ['notification-info']
    });
  }

  /**
   * Shows a warning notification with orange styling.
   */
  warning(message: string, duration = 4000): void {
    this.snackBar.open(message, 'Close', {
      ...this.defaultConfig,
      duration,
      panelClass: ['notification-warning']
    });
  }

  /**
   * Dismisses all currently displayed notifications.
   */
  dismiss(): void {
    this.snackBar.dismiss();
  }
}
