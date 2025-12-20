import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { throwError, Observable } from 'rxjs';
import { ApiErrorResponse } from '../models';
import { NotificationService } from './notification.service';

/**
 * Centralized error handling for API requests.
 * Converts HTTP errors to user-friendly messages and handles notifications.
 */
export class ApiErrorHandler {
  private notificationService = inject(NotificationService);

  /**
   * Handles HTTP errors and converts them to ApiErrorResponse.
   * Shows notification to user if showNotification is true.
   */
  handleError(error: HttpErrorResponse, showNotification = true): Observable<never> {
    const apiError = this.extractApiError(error);

    if (showNotification) {
      this.notificationService.error(apiError.message);
    }

    return throwError(() => apiError);
  }

  /**
   * Extracts API error response from HTTP error.
   */
  private extractApiError(error: HttpErrorResponse): ApiErrorResponse {
    // Server returned an error response
    if (error.error && typeof error.error === 'object' && 'message' in error.error) {
      return error.error as ApiErrorResponse;
    }

    // Client-side or network error
    if (error.error instanceof ErrorEvent) {
      return {
        message: `Network error: ${error.error.message}`,
        code: 'NETWORK_ERROR'
      };
    }

    // HTTP error without API error response
    return {
      message: this.getDefaultErrorMessage(error.status),
      code: `HTTP_${error.status}`,
      traceId: error.headers.get('X-Trace-Id') || undefined
    };
  }

  /**
   * Gets a user-friendly error message based on HTTP status code.
   */
  private getDefaultErrorMessage(status: number): string {
    switch (status) {
      case 0:
        return 'Unable to connect to the server. Please check your connection.';
      case 400:
        return 'Invalid request. Please check your input.';
      case 401:
        return 'You are not authorized. Please log in.';
      case 403:
        return 'You do not have permission to perform this action.';
      case 404:
        return 'The requested resource was not found.';
      case 409:
        return 'This operation conflicts with existing data.';
      case 500:
        return 'An internal server error occurred. Please try again later.';
      case 503:
        return 'The service is temporarily unavailable. Please try again later.';
      default:
        return `An unexpected error occurred (${status}).`;
    }
  }

  /**
   * Formats validation errors from API error response.
   */
  formatValidationErrors(errors: { [key: string]: string[] } | undefined): string {
    if (!errors) {
      return '';
    }

    const messages = Object.entries(errors)
      .map(([field, messages]) => `${field}: ${messages.join(', ')}`)
      .join('; ');

    return messages;
  }
}
