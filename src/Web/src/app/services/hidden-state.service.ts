import { Injectable, inject, signal } from '@angular/core';
import { HiddenFolderService } from './hidden-folder.service';

const SHOW_HIDDEN_STORAGE_KEY = 'showHidden';

/**
 * Service for managing the hidden files visibility state.
 * Persists the showHidden toggle state in localStorage.
 */
@Injectable({
  providedIn: 'root'
})
export class HiddenStateService {
  private hiddenFolderService = inject(HiddenFolderService);

  /**
   * Whether hidden files should be shown in the views.
   */
  readonly showHidden = signal<boolean>(this.loadShowHiddenState());

  /**
   * Count of hidden files (for badge display).
   */
  readonly hiddenFilesCount = signal<number>(0);

  constructor() {
    this.loadHiddenFilesCount();
  }

  /**
   * Toggles the showHidden state and persists it.
   */
  toggleShowHidden(): void {
    const newValue = !this.showHidden();
    this.showHidden.set(newValue);
    this.saveShowHiddenState(newValue);
  }

  /**
   * Sets the showHidden state and persists it.
   */
  setShowHidden(value: boolean): void {
    this.showHidden.set(value);
    this.saveShowHiddenState(value);
  }

  /**
   * Refreshes the hidden files count from the API.
   */
  refreshHiddenFilesCount(): void {
    this.loadHiddenFilesCount();
  }

  private loadShowHiddenState(): boolean {
    try {
      const stored = localStorage.getItem(SHOW_HIDDEN_STORAGE_KEY);
      return stored === 'true';
    } catch {
      return false;
    }
  }

  private saveShowHiddenState(value: boolean): void {
    try {
      localStorage.setItem(SHOW_HIDDEN_STORAGE_KEY, value.toString());
    } catch {
      // Ignore localStorage errors (e.g., in private browsing mode)
    }
  }

  private loadHiddenFilesCount(): void {
    this.hiddenFolderService.getHiddenFileCount().subscribe({
      next: (result) => {
        this.hiddenFilesCount.set(result.count);
      },
      error: () => {
        // Silently ignore errors, count stays at 0
        this.hiddenFilesCount.set(0);
      }
    });
  }
}
