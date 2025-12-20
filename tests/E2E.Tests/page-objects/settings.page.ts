import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './base.page';

/**
 * Page object for the Settings page
 * Manages scan directories and application configuration
 */
export class SettingsPage extends BasePage {
  readonly url = '/settings';

  // Directory management
  readonly addDirectoryButton: Locator;
  readonly directoryList: Locator;
  readonly directoryRows: Locator;

  // Directory form fields
  readonly pathInput: Locator;
  readonly descriptionInput: Locator;
  readonly enabledToggle: Locator;

  // Form actions
  readonly saveButton: Locator;
  readonly cancelButton: Locator;

  // Settings sections
  readonly generalSettings: Locator;
  readonly scanSettings: Locator;
  readonly storageSettings: Locator;

  constructor(page: Page) {
    super(page);

    // Directory management
    this.addDirectoryButton = page.getByRole('button', { name: /add directory/i });
    this.directoryList = page.locator('.directory-list, [data-testid="directory-list"]');
    this.directoryRows = page.locator('.directory-row, [data-testid="directory-row"]');

    // Form inputs
    this.pathInput = page.locator('input[formcontrolname="path"], input[name="path"]');
    this.descriptionInput = page.locator('input[formcontrolname="description"], textarea[name="description"]');
    this.enabledToggle = page.locator('mat-slide-toggle, input[type="checkbox"][formcontrolname="isEnabled"]');

    // Form buttons
    this.saveButton = page.getByRole('button', { name: /save|add/i });
    this.cancelButton = page.getByRole('button', { name: /cancel/i });

    // Settings sections
    this.generalSettings = page.locator('[data-testid="general-settings"]');
    this.scanSettings = page.locator('[data-testid="scan-settings"]');
    this.storageSettings = page.locator('[data-testid="storage-settings"]');
  }

  /**
   * Navigate to the settings page
   */
  async goto(): Promise<void> {
    await this.page.goto(this.url);
    await this.waitForPageLoad();
  }

  /**
   * Add a new scan directory
   */
  async addDirectory(path: string, description?: string, enabled: boolean = true): Promise<void> {
    await this.addDirectoryButton.click();

    // Fill in the form
    await this.pathInput.fill(path);

    if (description) {
      const descriptionVisible = await this.descriptionInput.isVisible().catch(() => false);
      if (descriptionVisible) {
        await this.descriptionInput.fill(description);
      }
    }

    // Set enabled state if toggle is visible
    const toggleVisible = await this.enabledToggle.isVisible().catch(() => false);
    if (toggleVisible) {
      const isChecked = await this.enabledToggle.isChecked().catch(() => false);
      if (isChecked !== enabled) {
        await this.enabledToggle.click();
      }
    }

    // Save the directory
    await this.saveButton.click();
    await this.waitForPageLoad();
  }

  /**
   * Edit an existing directory
   */
  async editDirectory(oldPath: string, newPath: string, description?: string): Promise<void> {
    const row = this.directoryRows.filter({ hasText: oldPath });
    const editButton = row.getByRole('button', { name: /edit/i });

    await editButton.click();
    await this.pathInput.fill(newPath);

    if (description) {
      await this.descriptionInput.fill(description);
    }

    await this.saveButton.click();
    await this.waitForPageLoad();
  }

  /**
   * Delete a directory
   */
  async deleteDirectory(path: string): Promise<void> {
    const row = this.directoryRows.filter({ hasText: path });
    const deleteButton = row.getByRole('button', { name: /delete|remove/i });

    await deleteButton.click();

    // Confirm deletion if confirmation dialog appears
    const confirmButton = this.page.getByRole('button', { name: /confirm|yes|delete/i });
    const confirmVisible = await confirmButton.isVisible({ timeout: 2000 }).catch(() => false);

    if (confirmVisible) {
      await confirmButton.click();
    }

    await this.waitForPageLoad();
  }

  /**
   * Toggle a directory's enabled state
   */
  async toggleDirectory(path: string): Promise<void> {
    const row = this.directoryRows.filter({ hasText: path });
    const toggle = row.locator('mat-slide-toggle, input[type="checkbox"]');

    await toggle.click();
    await this.waitForPageLoad();
  }

  /**
   * Get the number of configured directories
   */
  async getDirectoryCount(): Promise<number> {
    return await this.directoryRows.count();
  }

  /**
   * Get all directory paths
   */
  async getDirectoryPaths(): Promise<string[]> {
    const paths: string[] = [];
    const count = await this.directoryRows.count();

    for (let i = 0; i < count; i++) {
      const text = await this.directoryRows.nth(i).textContent();
      if (text) {
        paths.push(text.trim());
      }
    }

    return paths;
  }

  /**
   * Check if a directory exists in the list
   */
  async hasDirectory(path: string): Promise<boolean> {
    const row = this.directoryRows.filter({ hasText: path });
    return await row.count() > 0;
  }

  /**
   * Assert that the directory list is visible
   */
  async expectDirectoryListVisible(): Promise<void> {
    await expect(this.directoryList).toBeVisible();
  }

  /**
   * Assert that the add directory button is visible
   */
  async expectAddButtonVisible(): Promise<void> {
    await expect(this.addDirectoryButton).toBeVisible();
  }

  /**
   * Trigger a manual scan for a specific directory
   */
  async scanDirectory(path: string): Promise<void> {
    const row = this.directoryRows.filter({ hasText: path });
    const scanButton = row.getByRole('button', { name: /scan/i });

    const scanButtonVisible = await scanButton.isVisible().catch(() => false);
    if (scanButtonVisible) {
      await scanButton.click();
      await this.waitForPageLoad();
    }
  }
}
