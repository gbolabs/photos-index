import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';
import { vi, expect, describe, it, beforeEach } from 'vitest';
import { ApiErrorHandler } from './api-error-handler';
import { NotificationService } from './notification.service';

describe('ApiErrorHandler', () => {
  let errorHandler: ApiErrorHandler;
  let notificationServiceMock: {
    error: ReturnType<typeof vi.fn>;
    success: ReturnType<typeof vi.fn>;
    warning: ReturnType<typeof vi.fn>;
    info: ReturnType<typeof vi.fn>;
    dismiss: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    notificationServiceMock = {
      error: vi.fn(),
      success: vi.fn(),
      warning: vi.fn(),
      info: vi.fn(),
      dismiss: vi.fn()
    };

    const snackBarMock = {
      open: vi.fn(),
      dismiss: vi.fn()
    };

    TestBed.configureTestingModule({
      providers: [
        ApiErrorHandler,
        { provide: NotificationService, useValue: notificationServiceMock },
        { provide: MatSnackBar, useValue: snackBarMock }
      ]
    });

    errorHandler = TestBed.inject(ApiErrorHandler);
  });

  it('should be created', () => {
    expect(errorHandler).toBeTruthy();
  });

  it('should extract API error from server response', () => {
    const apiError = {
      message: 'Validation failed',
      code: 'VALIDATION_ERROR',
      errors: { field1: ['Error 1', 'Error 2'] }
    };

    const httpError = new HttpErrorResponse({
      error: apiError,
      status: 400,
      statusText: 'Bad Request'
    });

    errorHandler.handleError(httpError).subscribe({
      error: (error) => {
        expect(error.message).toBe('Validation failed');
        expect(error.code).toBe('VALIDATION_ERROR');
        expect(notificationServiceMock.error).toHaveBeenCalledWith('Validation failed', 5000, undefined);
      }
    });
  });

  it('should handle network errors', () => {
    const errorEvent = new ErrorEvent('Network error', {
      message: 'Connection refused'
    });

    const httpError = new HttpErrorResponse({
      error: errorEvent,
      status: 0,
      statusText: 'Unknown Error'
    });

    errorHandler.handleError(httpError).subscribe({
      error: (error) => {
        expect(error.message).toContain('Network error');
        expect(error.code).toBe('NETWORK_ERROR');
      }
    });
  });

  it('should handle 404 errors with default message', () => {
    const httpError = new HttpErrorResponse({
      error: 'Not Found',
      status: 404,
      statusText: 'Not Found'
    });

    errorHandler.handleError(httpError).subscribe({
      error: (error) => {
        expect(error.message).toBe('The requested resource was not found.');
        expect(error.code).toBe('HTTP_404');
      }
    });
  });

  it('should handle 500 errors with default message', () => {
    const httpError = new HttpErrorResponse({
      error: 'Internal Server Error',
      status: 500,
      statusText: 'Internal Server Error'
    });

    errorHandler.handleError(httpError).subscribe({
      error: (error) => {
        expect(error.message).toBe(
          'An internal server error occurred. Please try again later.'
        );
        expect(error.code).toBe('HTTP_500');
      }
    });
  });

  it('should not show notification when showNotification is false', () => {
    const httpError = new HttpErrorResponse({
      error: 'Not Found',
      status: 404,
      statusText: 'Not Found'
    });

    errorHandler.handleError(httpError, false).subscribe({
      error: () => {
        expect(notificationServiceMock.error).not.toHaveBeenCalled();
      }
    });
  });

  it('should format validation errors correctly', () => {
    const errors = {
      email: ['Email is required', 'Email format is invalid'],
      password: ['Password must be at least 8 characters']
    };

    const formatted = errorHandler.formatValidationErrors(errors);

    expect(formatted).toContain('email: Email is required, Email format is invalid');
    expect(formatted).toContain('password: Password must be at least 8 characters');
  });

  it('should handle undefined validation errors', () => {
    const formatted = errorHandler.formatValidationErrors(undefined);
    expect(formatted).toBe('');
  });

  it('should handle 401 unauthorized errors', () => {
    const httpError = new HttpErrorResponse({
      error: 'Unauthorized',
      status: 401,
      statusText: 'Unauthorized'
    });

    errorHandler.handleError(httpError).subscribe({
      error: (error) => {
        expect(error.message).toBe('You are not authorized. Please log in.');
        expect(error.code).toBe('HTTP_401');
      }
    });
  });

  it('should handle 403 forbidden errors', () => {
    const httpError = new HttpErrorResponse({
      error: 'Forbidden',
      status: 403,
      statusText: 'Forbidden'
    });

    errorHandler.handleError(httpError).subscribe({
      error: (error) => {
        expect(error.message).toBe('You do not have permission to perform this action.');
        expect(error.code).toBe('HTTP_403');
      }
    });
  });

  it('should handle 409 conflict errors', () => {
    const httpError = new HttpErrorResponse({
      error: 'Conflict',
      status: 409,
      statusText: 'Conflict'
    });

    errorHandler.handleError(httpError).subscribe({
      error: (error) => {
        expect(error.message).toBe('This operation conflicts with existing data.');
        expect(error.code).toBe('HTTP_409');
      }
    });
  });
});
