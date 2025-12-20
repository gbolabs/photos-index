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
   * Includes trace ID in notification for debugging.
   */
  handleError(error: HttpErrorResponse, showNotification = true): Observable<never> {
    const apiError = this.extractApiError(error);

    if (showNotification) {
      // Include trace ID in error notification for debugging
      this.notificationService.error(apiError.message, 5000, apiError.traceId);
    }

    return throwError(() => apiError);
  }

  /**
   * Extracts API error response from HTTP error.
   * Always attempts to extract trace ID from headers for correlation.
   */
  private extractApiError(error: HttpErrorResponse): ApiErrorResponse {
    // Get trace ID from response headers (always try to extract)
    const headerTraceId = error.headers?.get('X-Trace-Id') || undefined;

    // Client-side or network error (check first - ErrorEvent also has 'message' property)
    if (error.error instanceof ErrorEvent) {
      return {
        message: `Network error: ${error.error.message}`,
        code: 'NETWORK_ERROR',
        traceId: headerTraceId
      };
    }

    // Server returned an error response with body
    if (error.error && typeof error.error === 'object' && 'message' in error.error) {
      const apiError = error.error as ApiErrorResponse;
      // Use trace ID from body if available, otherwise from header
      return {
        ...apiError,
        traceId: apiError.traceId || headerTraceId
      };
    }

    // HTTP error without API error response body
    return {
      message: this.getDefaultErrorMessage(error.status),
      code: `HTTP_${error.status}`,
      traceId: headerTraceId
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
