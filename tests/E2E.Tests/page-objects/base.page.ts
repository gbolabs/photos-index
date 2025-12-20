import { Page, Locator } from '@playwright/test';

/**
 * Base page object containing common navigation and utilities
 * All page objects should extend this class
 */
export abstract class BasePage {
  readonly page: Page;

  // Common navigation elements
  readonly navDashboard: Locator;
  readonly navSettings: Locator;
  readonly navFiles: Locator;
  readonly navDuplicates: Locator;

  // Common UI elements
  readonly loadingSpinner: Locator;
  readonly errorMessage: Locator;
  readonly successMessage: Locator;

  constructor(page: Page) {
    this.page = page;

    // Navigation links - adjust selectors based on actual Angular implementation
    this.navDashboard = page.getByRole('link', { name: 'Dashboard' });
    this.navSettings = page.getByRole('link', { name: 'Settings' });
    this.navFiles = page.getByRole('link', { name: 'Files' });
    this.navDuplicates = page.getByRole('link', { name: 'Duplicates' });

    // Common UI elements
    this.loadingSpinner = page.locator('mat-spinner, .loading-spinner');
    this.errorMessage = page.locator('.error-message, mat-error');
    this.successMessage = page.locator('.success-message, mat-snack-bar-container');
  }

  /**
   * Wait for page to finish loading
   * Override this method in child classes if needed
   */
  async waitForPageLoad(): Promise<void> {
    // Wait for loading spinner to disappear
    await this.page.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {
      // Ignore timeout, continue anyway
    });

    // Wait for spinner to be hidden if it exists
    const spinnerCount = await this.loadingSpinner.count();
    if (spinnerCount > 0) {
      await this.loadingSpinner.first().waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {
        // Ignore if spinner doesn't disappear
      });
    }
  }

  /**
   * Navigate to Dashboard page
   */
  async navigateToDashboard(): Promise<void> {
    await this.navDashboard.click();
    await this.waitForPageLoad();
  }

  /**
   * Navigate to Settings page
   */
  async navigateToSettings(): Promise<void> {
    await this.navSettings.click();
    await this.waitForPageLoad();
  }

  /**
   * Navigate to Files page
   */
  async navigateToFiles(): Promise<void> {
    await this.navFiles.click();
    await this.waitForPageLoad();
  }

  /**
   * Navigate to Duplicates page
   */
  async navigateToDuplicates(): Promise<void> {
    await this.navDuplicates.click();
    await this.waitForPageLoad();
  }

  /**
   * Check if error message is displayed
   */
  async hasError(): Promise<boolean> {
    return await this.errorMessage.isVisible();
  }

  /**
   * Get error message text
   */
  async getErrorMessage(): Promise<string> {
    return await this.errorMessage.textContent() ?? '';
  }

  /**
   * Check if success message is displayed
   */
  async hasSuccess(): Promise<boolean> {
    return await this.successMessage.isVisible();
  }

  /**
   * Get success message text
   */
  async getSuccessMessage(): Promise<string> {
    return await this.successMessage.textContent() ?? '';
  }

  /**
   * Take a screenshot with a meaningful name
   */
  async takeScreenshot(name: string): Promise<void> {
    await this.page.screenshot({
      path: `screenshots/${name}.png`,
      fullPage: true
    });
  }
}
