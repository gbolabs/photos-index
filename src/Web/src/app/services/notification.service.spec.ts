import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { vi } from 'vitest';
import { NotificationService } from './notification.service';

describe('NotificationService', () => {
  let service: NotificationService;
  let snackBarMock: { open: ReturnType<typeof vi.fn>; dismiss: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    snackBarMock = {
      open: vi.fn(),
      dismiss: vi.fn()
    };

    TestBed.configureTestingModule({
      providers: [NotificationService, { provide: MatSnackBar, useValue: snackBarMock }]
    });

    service = TestBed.inject(NotificationService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should show success notification with correct config', () => {
    const message = 'Operation successful';

    service.success(message);

    expect(snackBarMock.open).toHaveBeenCalledWith(message, 'Close', {
      duration: 3000,
      horizontalPosition: 'end',
      verticalPosition: 'bottom',
      panelClass: ['notification-success']
    });
  });

  it('should show error notification with longer duration', () => {
    const message = 'An error occurred';

    service.error(message);

    expect(snackBarMock.open).toHaveBeenCalledWith(message, 'Close', {
      duration: 5000,
      horizontalPosition: 'end',
      verticalPosition: 'bottom',
      panelClass: ['notification-error']
    });
  });

  it('should show info notification', () => {
    const message = 'Information message';

    service.info(message);

    expect(snackBarMock.open).toHaveBeenCalledWith(message, 'Close', {
      duration: 3000,
      horizontalPosition: 'end',
      verticalPosition: 'bottom',
      panelClass: ['notification-info']
    });
  });

  it('should show warning notification', () => {
    const message = 'Warning message';

    service.warning(message);

    expect(snackBarMock.open).toHaveBeenCalledWith(message, 'Close', {
      duration: 4000,
      horizontalPosition: 'end',
      verticalPosition: 'bottom',
      panelClass: ['notification-warning']
    });
  });

  it('should allow custom duration for success notification', () => {
    const message = 'Custom duration';
    const duration = 10000;

    service.success(message, duration);

    expect(snackBarMock.open).toHaveBeenCalledWith(message, 'Close', {
      duration: 10000,
      horizontalPosition: 'end',
      verticalPosition: 'bottom',
      panelClass: ['notification-success']
    });
  });

  it('should dismiss all notifications', () => {
    service.dismiss();

    expect(snackBarMock.dismiss).toHaveBeenCalled();
  });
});
