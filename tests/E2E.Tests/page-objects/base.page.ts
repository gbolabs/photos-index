import { Page, Locator } from '@playwright/test';

/**
 * Base page object containing common navigation and utilities
 * All page objects should extend this class
 */
export abstract class BasePage {
  readonly page: Page;

  // Common navigation elements
  readonly navDashboard: Locator;
  readonly navIndexing: Locator;
  readonly navSettings: Locator;
  readonly navFiles: Locator;
  readonly navDuplicates: Locator;

  // Common UI elements
  readonly loadingSpinner: Locator;
  readonly errorMessage: Locator;
  readonly successMessage: Locator;

  // Menu button for opening drawer
  readonly menuButton: Locator;

  constructor(page: Page) {
    this.page = page;

    // Menu button to open the navigation drawer - button containing menu icon in toolbar
    this.menuButton = page.locator('mat-toolbar button').filter({ has: page.locator('mat-icon:text("menu")') }).first();

    // Navigation links inside mat-drawer/mat-nav-list
    this.navDashboard = page.locator('mat-nav-list a').filter({ hasText: 'Dashboard' }).first();
    this.navIndexing = page.locator('mat-nav-list a').filter({ hasText: 'Indexing' }).first();
    this.navSettings = page.locator('mat-nav-list a').filter({ hasText: 'Settings' }).first();
    this.navFiles = page.locator('mat-nav-list a').filter({ hasText: 'Files' }).first();
    this.navDuplicates = page.locator('mat-nav-list a').filter({ hasText: 'Duplicates' }).first();

    // Common UI elements
    this.loadingSpinner = page.locator('mat-spinner, .loading-spinner');
    this.errorMessage = page.locator('.error-message, mat-error');
    this.successMessage = page.locator('.success-message, mat-snack-bar-container');
  }

  /**
   * Open the navigation drawer if not already open
   */
  async openDrawer(): Promise<void> {
    // Check if drawer content is visible (nav links)
    const isOpen = await this.navDashboard.isVisible().catch(() => false);

    if (!isOpen) {
      await this.menuButton.click();
      // Wait for drawer animation and nav to be visible
      await this.navDashboard.waitFor({ state: 'visible', timeout: 5000 });
    }
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
    await this.openDrawer();
    await this.navDashboard.click();
    await this.waitForPageLoad();
  }

  /**
   * Navigate to Indexing page
   */
  async navigateToIndexing(): Promise<void> {
    await this.openDrawer();
    await this.navIndexing.click();
    await this.waitForPageLoad();
  }

  /**
   * Navigate to Settings page
   */
  async navigateToSettings(): Promise<void> {
    await this.openDrawer();
    await this.navSettings.click();
    await this.waitForPageLoad();
  }

  /**
   * Navigate to Files page
   */
  async navigateToFiles(): Promise<void> {
    await this.openDrawer();
    await this.navFiles.click();
    await this.waitForPageLoad();
  }

  /**
   * Navigate to Duplicates page
   */
  async navigateToDuplicates(): Promise<void> {
    await this.openDrawer();
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
