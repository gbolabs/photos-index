import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './base.page';

/**
 * Page object for the Indexing page
 * Monitor and control directory scanning
 */
export class IndexingPage extends BasePage {
  readonly url = '/indexing';

  // Header actions
  readonly scanAllButton: Locator;
  readonly refreshButton: Locator;

  // Status indicators
  readonly indexingStatusCard: Locator;
  readonly progressBar: Locator;
  readonly elapsedTime: Locator;
  readonly filesScanned: Locator;
  readonly filesIngested: Locator;
  readonly filesFailed: Locator;
  readonly progressPercentage: Locator;

  // Summary cards
  readonly totalDirectoriesCard: Locator;
  readonly enabledDirectoriesCard: Locator;
  readonly totalFilesCard: Locator;

  // Directory list
  readonly directoryCards: Locator;
  readonly scanDirectoryButtons: Locator;

  // Empty state
  readonly emptyState: Locator;
  readonly goToSettingsButton: Locator;

  // Auto-refresh indicator
  readonly autoRefreshIndicator: Locator;

  constructor(page: Page) {
    super(page);

    // Header actions
    this.scanAllButton = page.getByRole('button', { name: /scan all/i });
    this.refreshButton = page.locator('.header-actions button[mattooltip="Refresh"]');

    // Status indicators
    this.indexingStatusCard = page.locator('.indexing-status-card');
    this.progressBar = page.locator('mat-progress-bar');
    this.elapsedTime = page.locator('.status-elapsed');
    this.filesScanned = page.locator('.stat:has-text("Scanned") .stat-value');
    this.filesIngested = page.locator('.stat:has-text("Ingested") .stat-value');
    this.filesFailed = page.locator('.stat:has-text("Failed") .stat-value');
    this.progressPercentage = page.locator('.stat:has-text("Progress") .stat-value');

    // Summary cards
    this.totalDirectoriesCard = page.locator('.summary-card:has-text("Total Directories")');
    this.enabledDirectoriesCard = page.locator('.summary-card:has-text("Enabled")');
    this.totalFilesCard = page.locator('.summary-card:has-text("Total Files")');

    // Directory list
    this.directoryCards = page.locator('.directory-card');
    this.scanDirectoryButtons = page.locator('.directory-card button[mattooltip="Scan this directory"]');

    // Empty state
    this.emptyState = page.locator('.empty-card');
    this.goToSettingsButton = page.locator('.empty-card a[routerlink="/settings"]');

    // Auto-refresh
    this.autoRefreshIndicator = page.locator('.auto-refresh-indicator');
  }

  /**
   * Navigate to the indexing page
   */
  async goto(): Promise<void> {
    await this.page.goto(this.url);
    await this.waitForPageLoad();
  }

  /**
   * Start scanning all directories
   */
  async scanAll(): Promise<void> {
    await this.scanAllButton.click();
    // Wait a moment for the scan to start
    await this.page.waitForTimeout(500);
  }

  /**
   * Start scanning a specific directory by index
   */
  async scanDirectory(index: number): Promise<void> {
    await this.scanDirectoryButtons.nth(index).click();
    await this.page.waitForTimeout(500);
  }

  /**
   * Refresh the page data
   */
  async refresh(): Promise<void> {
    await this.refreshButton.click();
    await this.waitForPageLoad();
  }

  /**
   * Get the number of directories displayed
   */
  async getDirectoryCount(): Promise<number> {
    return await this.directoryCards.count();
  }

  /**
   * Check if indexing is in progress
   */
  async isIndexing(): Promise<boolean> {
    return await this.indexingStatusCard.isVisible().catch(() => false);
  }

  /**
   * Get the total directories count from summary
   */
  async getTotalDirectoriesCount(): Promise<string> {
    const card = this.totalDirectoriesCard.locator('.summary-value');
    return await card.textContent() ?? '0';
  }

  /**
   * Get the enabled directories count from summary
   */
  async getEnabledDirectoriesCount(): Promise<string> {
    const card = this.enabledDirectoriesCard.locator('.summary-value');
    return await card.textContent() ?? '0';
  }

  /**
   * Get total files count from summary
   */
  async getTotalFilesCount(): Promise<string> {
    const card = this.totalFilesCard.locator('.summary-value');
    return await card.textContent() ?? '0';
  }

  /**
   * Check if a directory is enabled
   */
  async isDirectoryEnabled(index: number): Promise<boolean> {
    const card = this.directoryCards.nth(index);
    const disabledClass = await card.getAttribute('class');
    return !disabledClass?.includes('disabled');
  }

  /**
   * Check if a directory is currently scanning
   */
  async isDirectoryScanning(index: number): Promise<boolean> {
    const card = this.directoryCards.nth(index);
    const classes = await card.getAttribute('class');
    return classes?.includes('scanning') ?? false;
  }

  /**
   * Get directory path by index
   */
  async getDirectoryPath(index: number): Promise<string> {
    const path = this.directoryCards.nth(index).locator('.directory-path');
    return await path.textContent() ?? '';
  }

  /**
   * Assert that summary cards are visible
   */
  async expectSummaryVisible(): Promise<void> {
    await expect(this.totalDirectoriesCard).toBeVisible({ timeout: 10000 });
    await expect(this.enabledDirectoriesCard).toBeVisible();
    await expect(this.totalFilesCard).toBeVisible();
  }

  /**
   * Assert that the directory list is visible
   */
  async expectDirectoryListVisible(): Promise<void> {
    const hasDirectories = await this.directoryCards.count() > 0;
    const hasEmptyState = await this.emptyState.isVisible().catch(() => false);
    expect(hasDirectories || hasEmptyState).toBeTruthy();
  }

  /**
   * Assert that auto-refresh indicator is visible
   */
  async expectAutoRefreshVisible(): Promise<void> {
    await expect(this.autoRefreshIndicator).toBeVisible();
  }

  /**
   * Wait for indexing to complete (with timeout)
   */
  async waitForIndexingComplete(timeoutMs: number = 60000): Promise<void> {
    await this.page.waitForFunction(
      () => !document.querySelector('.indexing-status-card'),
      { timeout: timeoutMs }
    );
  }
}
