import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './base.page';

/**
 * Page object for the Dashboard page
 * Contains all dashboard-specific elements and actions
 */
export class DashboardPage extends BasePage {
  readonly url = '/';

  // Statistics cards
  readonly totalFilesCard: Locator;
  readonly duplicatesCard: Locator;
  readonly savingsCard: Locator;
  readonly directoriesCard: Locator;

  // Actions
  readonly refreshButton: Locator;
  readonly scanButton: Locator;

  // Directory cards
  readonly directoryCards: Locator;
  readonly directoryCardTitles: Locator;

  // Recent activity
  readonly recentActivity: Locator;

  constructor(page: Page) {
    super(page);

    // Statistics cards - use data-testid attributes for reliable selection
    this.totalFilesCard = page.locator('[data-testid="total-files"]');
    this.duplicatesCard = page.locator('[data-testid="duplicates"]');
    this.savingsCard = page.locator('[data-testid="savings"]');
    this.directoriesCard = page.locator('[data-testid="directories"]');

    // Action buttons
    this.refreshButton = page.getByRole('button', { name: /refresh/i });
    this.scanButton = page.getByRole('button', { name: /scan/i });

    // Directory sections
    this.directoryCards = page.locator('.directory-card, mat-card.directory-card, [data-testid="directory-card"]');
    this.directoryCardTitles = page.locator('.directory-card-title, .path, [data-testid="directory-title"]');

    // Recent activity
    this.recentActivity = page.locator('[data-testid="recent-activity"], .recent-activity');
  }

  /**
   * Navigate to the dashboard page
   */
  async goto(): Promise<void> {
    await this.page.goto(this.url);
    await this.waitForPageLoad();
  }

  /**
   * Get the total number of files displayed
   */
  async getTotalFiles(): Promise<string> {
    return await this.totalFilesCard.textContent() ?? '';
  }

  /**
   * Get the directories count
   */
  async getDirectoriesCount(): Promise<string> {
    return await this.directoriesCard.textContent() ?? '';
  }

  /**
   * Get the duplicate count
   */
  async getDuplicateCount(): Promise<string> {
    return await this.duplicatesCard.textContent() ?? '';
  }

  /**
   * Get the potential savings value
   */
  async getSavings(): Promise<string> {
    return await this.savingsCard.textContent() ?? '';
  }

  /**
   * Click the refresh button to reload data
   */
  async refresh(): Promise<void> {
    await this.refreshButton.click();
    await this.waitForPageLoad();
  }

  /**
   * Click the scan button to start a new scan
   */
  async startScan(): Promise<void> {
    const scanButtonVisible = await this.scanButton.isVisible();
    if (scanButtonVisible) {
      await this.scanButton.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Get the number of directory cards displayed
   */
  async getDirectoryCardCount(): Promise<number> {
    return await this.directoryCards.count();
  }

  /**
   * Get all directory titles
   */
  async getDirectoryTitles(): Promise<string[]> {
    const titles: string[] = [];
    const count = await this.directoryCardTitles.count();

    for (let i = 0; i < count; i++) {
      const text = await this.directoryCardTitles.nth(i).textContent();
      if (text) {
        titles.push(text.trim());
      }
    }

    return titles;
  }

  /**
   * Click on a specific directory card
   */
  async clickDirectoryCard(index: number): Promise<void> {
    await this.directoryCards.nth(index).click();
    await this.waitForPageLoad();
  }

  /**
   * Assert that all statistics cards are visible
   */
  async expectStatsVisible(): Promise<void> {
    await expect(this.totalFilesCard).toBeVisible({ timeout: 10000 });
    await expect(this.duplicatesCard).toBeVisible();
    await expect(this.savingsCard).toBeVisible();
    await expect(this.directoriesCard).toBeVisible();
  }

  /**
   * Assert that the page title is correct
   */
  async expectPageTitle(): Promise<void> {
    await expect(this.page).toHaveTitle(/Dashboard|Photos\s*Index/i);
  }

  /**
   * Check if recent activity section is visible
   */
  async hasRecentActivity(): Promise<boolean> {
    return await this.recentActivity.isVisible().catch(() => false);
  }
}
