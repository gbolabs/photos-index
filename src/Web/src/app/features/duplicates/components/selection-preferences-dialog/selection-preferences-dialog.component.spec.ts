import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';

import { SelectionPreferencesDialogComponent } from './selection-preferences-dialog.component';
import {
  SelectionPreferencesService,
  SelectionConfigDto,
} from '../../../../services/selection-preferences.service';

describe('SelectionPreferencesDialogComponent', () => {
  let component: SelectionPreferencesDialogComponent;
  let fixture: ComponentFixture<SelectionPreferencesDialogComponent>;
  let mockPreferencesService: {
    getConfig: ReturnType<typeof vi.fn>;
    getPreferences: ReturnType<typeof vi.fn>;
    savePreferences: ReturnType<typeof vi.fn>;
    resetToDefaults: ReturnType<typeof vi.fn>;
    recalculateOriginals: ReturnType<typeof vi.fn>;
    getFileScore: ReturnType<typeof vi.fn>;
  };
  let mockDialogRef: { close: ReturnType<typeof vi.fn> };
  let mockSnackBar: { open: ReturnType<typeof vi.fn> };

  const mockConfig: SelectionConfigDto = {
    pathPriorities: [
      { id: '1', pathPrefix: '/photos/originals', priority: 80, sortOrder: 0 },
      { id: '2', pathPrefix: '/photos/backup', priority: 20, sortOrder: 1 },
    ],
    preferExifData: true,
    preferDeeperPaths: true,
    preferOlderFiles: true,
    conflictThreshold: 5,
  };

  beforeEach(async () => {
    mockPreferencesService = {
      getConfig: vi.fn().mockReturnValue(of(mockConfig)),
      getPreferences: vi.fn().mockReturnValue(of(mockConfig.pathPriorities)),
      savePreferences: vi.fn().mockReturnValue(of(void 0)),
      resetToDefaults: vi.fn().mockReturnValue(of(void 0)),
      recalculateOriginals: vi.fn().mockReturnValue(of({ updated: 10, conflicts: 2 })),
      getFileScore: vi.fn(),
    };

    mockDialogRef = {
      close: vi.fn(),
    };

    mockSnackBar = {
      open: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [SelectionPreferencesDialogComponent, NoopAnimationsModule],
      providers: [
        { provide: SelectionPreferencesService, useValue: mockPreferencesService },
        { provide: MatDialogRef, useValue: mockDialogRef },
        { provide: MatSnackBar, useValue: mockSnackBar },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SelectionPreferencesDialogComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load config on init', () => {
    fixture.detectChanges();

    expect(mockPreferencesService.getConfig).toHaveBeenCalled();
    expect(component.config()).toEqual(mockConfig);
    expect(component.preferences().length).toBe(2);
    expect(component.loading()).toBe(false);
  });

  it('should handle config load error', () => {
    mockPreferencesService.getConfig.mockReturnValue(
      throwError(() => new Error('Failed to load'))
    );

    fixture.detectChanges();

    expect(component.loading()).toBe(false);
  });

  it('should add preference', () => {
    fixture.detectChanges();
    const initialCount = component.preferences().length;

    component.newPathPrefix.set('/photos/new');
    component.newPriority.set(60);
    component.addPreference();

    expect(component.preferences().length).toBe(initialCount + 1);
    expect(component.preferences()[initialCount].pathPrefix).toBe('/photos/new');
    expect(component.preferences()[initialCount].priority).toBe(60);
    expect(component.hasChanges()).toBe(true);
    expect(component.newPathPrefix()).toBe('');
  });

  it('should not add preference without path prefix', () => {
    fixture.detectChanges();
    const initialCount = component.preferences().length;

    component.newPathPrefix.set('');
    component.addPreference();

    expect(component.preferences().length).toBe(initialCount);
  });

  it('should not add duplicate path prefix', () => {
    fixture.detectChanges();
    const initialCount = component.preferences().length;

    component.newPathPrefix.set('/photos/originals');
    component.addPreference();

    expect(component.preferences().length).toBe(initialCount);
  });

  it('should remove preference', () => {
    fixture.detectChanges();
    const initialCount = component.preferences().length;
    const prefToRemove = component.preferences()[0];

    component.removePreference(prefToRemove);

    expect(component.preferences().length).toBe(initialCount - 1);
    expect(component.preferences().find((p) => p.id === prefToRemove.id)).toBeUndefined();
    expect(component.hasChanges()).toBe(true);
  });

  it('should update preference priority', () => {
    fixture.detectChanges();
    const pref = component.preferences()[0];

    component.updatePriority(pref, 90);

    expect(component.preferences()[0].priority).toBe(90);
    expect(component.hasChanges()).toBe(true);
  });

  it('should save preferences', () => {
    fixture.detectChanges();
    component.hasChanges.set(true);

    component.save();

    expect(mockPreferencesService.savePreferences).toHaveBeenCalledWith(
      component.preferences()
    );
    expect(component.hasChanges()).toBe(false);
  });

  it('should handle save error', () => {
    mockPreferencesService.savePreferences.mockReturnValue(
      throwError(() => new Error('Save failed'))
    );
    fixture.detectChanges();
    component.hasChanges.set(true);

    component.save();

    expect(component.saving()).toBe(false);
  });

  it('should reset to defaults with confirmation', () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    fixture.detectChanges();

    component.resetToDefaults();

    expect(confirmSpy).toHaveBeenCalled();
    expect(mockPreferencesService.resetToDefaults).toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it('should not reset to defaults without confirmation', () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    fixture.detectChanges();

    component.resetToDefaults();

    expect(confirmSpy).toHaveBeenCalled();
    expect(mockPreferencesService.resetToDefaults).not.toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it('should recalculate all with confirmation', () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    fixture.detectChanges();

    component.recalculateAll();

    expect(confirmSpy).toHaveBeenCalled();
    expect(mockPreferencesService.recalculateOriginals).toHaveBeenCalledWith({
      scope: 'all',
    });
    confirmSpy.mockRestore();
  });

  it('should close dialog', () => {
    fixture.detectChanges();
    component.hasChanges.set(false);

    component.close();

    expect(mockDialogRef.close).toHaveBeenCalled();
  });

  it('should confirm before closing with unsaved changes', () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    fixture.detectChanges();
    component.hasChanges.set(true);

    component.close();

    expect(confirmSpy).toHaveBeenCalled();
    expect(mockDialogRef.close).toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  it('should not close when user cancels discard', () => {
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    fixture.detectChanges();
    component.hasChanges.set(true);

    component.close();

    expect(confirmSpy).toHaveBeenCalled();
    expect(mockDialogRef.close).not.toHaveBeenCalled();
    confirmSpy.mockRestore();
  });

  describe('getPriorityLabel', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should return High for priority >= 80', () => {
      expect(component.getPriorityLabel(80)).toBe('High');
      expect(component.getPriorityLabel(100)).toBe('High');
    });

    it('should return Medium for priority >= 50', () => {
      expect(component.getPriorityLabel(50)).toBe('Medium');
      expect(component.getPriorityLabel(79)).toBe('Medium');
    });

    it('should return Low for priority >= 20', () => {
      expect(component.getPriorityLabel(20)).toBe('Low');
      expect(component.getPriorityLabel(49)).toBe('Low');
    });

    it('should return Very Low for priority < 20', () => {
      expect(component.getPriorityLabel(0)).toBe('Very Low');
      expect(component.getPriorityLabel(19)).toBe('Very Low');
    });
  });
});
