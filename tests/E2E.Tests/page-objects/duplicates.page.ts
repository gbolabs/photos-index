import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './base.page';

/**
 * Page object for the Duplicates page
 * Manage and review duplicate files
 */
export class DuplicatesPage extends BasePage {
  readonly url = '/duplicates';

  // Duplicate groups
  readonly duplicateGroups: Locator;
  readonly groupItems: Locator;
  readonly expandedGroup: Locator;

  // Group actions
  readonly expandAllButton: Locator;
  readonly collapseAllButton: Locator;
  readonly selectAllButton: Locator;

  // File actions within a group
  readonly fileCheckboxes: Locator;
  readonly keepButton: Locator;
  readonly deleteButton: Locator;

  // Filters
  readonly filterBySize: Locator;
  readonly filterByDate: Locator;
  readonly filterByType: Locator;
  readonly minSizeInput: Locator;

  // Statistics
  readonly totalDuplicates: Locator;
  readonly potentialSavings: Locator;
  readonly groupCount: Locator;

  // Bulk actions
  readonly bulkActionDropdown: Locator;
  readonly autoSelectOldest: Locator;
  readonly autoSelectSmallest: Locator;
  readonly deleteSelectedButton: Locator;

  // Preview
  readonly imagePreview: Locator;
  readonly compareView: Locator;

  constructor(page: Page) {
    super(page);

    // Duplicate groups
    this.duplicateGroups = page.locator('.duplicate-groups, [data-testid="duplicate-groups"]');
    this.groupItems = page.locator('.duplicate-group, [data-testid="duplicate-group"]');
    this.expandedGroup = page.locator('.duplicate-group.expanded, [data-testid="duplicate-group"][aria-expanded="true"]');

    // Group actions
    this.expandAllButton = page.getByRole('button', { name: /expand all/i });
    this.collapseAllButton = page.getByRole('button', { name: /collapse all/i });
    this.selectAllButton = page.getByRole('button', { name: /select all/i });

    // File actions
    this.fileCheckboxes = page.locator('.file-checkbox, input[type="checkbox"][data-file-id]');
    this.keepButton = page.getByRole('button', { name: /keep/i });
    this.deleteButton = page.getByRole('button', { name: /delete/i });

    // Filters
    this.filterBySize = page.locator('mat-select[formcontrolname="sizeFilter"], select[name="sizeFilter"]');
    this.filterByDate = page.locator('mat-select[formcontrolname="dateFilter"], select[name="dateFilter"]');
    this.filterByType = page.locator('mat-select[formcontrolname="typeFilter"], select[name="typeFilter"]');
    this.minSizeInput = page.locator('input[formcontrolname="minSize"], input[name="minSize"]');

    // Statistics
    this.totalDuplicates = page.locator('[data-testid="total-duplicates"]');
    this.potentialSavings = page.locator('[data-testid="potential-savings"]');
    this.groupCount = page.locator('[data-testid="group-count"]');

    // Bulk actions
    this.bulkActionDropdown = page.locator('mat-select[formcontrolname="bulkAction"], select[name="bulkAction"]');
    this.autoSelectOldest = page.getByRole('button', { name: /auto.*oldest/i });
    this.autoSelectSmallest = page.getByRole('button', { name: /auto.*smallest/i });
    this.deleteSelectedButton = page.getByRole('button', { name: /delete selected/i });

    // Preview
    this.imagePreview = page.locator('.image-preview, [data-testid="image-preview"]');
    this.compareView = page.locator('.compare-view, [data-testid="compare-view"]');
  }

  /**
   * Navigate to the duplicates page
   */
  async goto(): Promise<void> {
    await this.page.goto(this.url);
    await this.waitForPageLoad();
  }

  /**
   * Get the number of duplicate groups
   */
  async getGroupCount(): Promise<number> {
    return await this.groupItems.count();
  }

  /**
   * Expand a duplicate group by index
   */
  async expandGroup(index: number): Promise<void> {
    const group = this.groupItems.nth(index);
    const expandButton = group.locator('button[aria-label*="expand" i], .expand-button');

    const isExpanded = await group.getAttribute('aria-expanded') === 'true';
    if (!isExpanded) {
      await expandButton.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Collapse a duplicate group by index
   */
  async collapseGroup(index: number): Promise<void> {
    const group = this.groupItems.nth(index);
    const collapseButton = group.locator('button[aria-label*="collapse" i], .collapse-button');

    const isExpanded = await group.getAttribute('aria-expanded') === 'true';
    if (isExpanded) {
      await collapseButton.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Expand all groups
   */
  async expandAll(): Promise<void> {
    const expandAllVisible = await this.expandAllButton.isVisible().catch(() => false);
    if (expandAllVisible) {
      await this.expandAllButton.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Collapse all groups
   */
  async collapseAll(): Promise<void> {
    const collapseAllVisible = await this.collapseAllButton.isVisible().catch(() => false);
    if (collapseAllVisible) {
      await this.collapseAllButton.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Select a file within a group for deletion
   */
  async selectFileForDeletion(groupIndex: number, fileIndex: number): Promise<void> {
    await this.expandGroup(groupIndex);

    const group = this.groupItems.nth(groupIndex);
    const fileCheckbox = group.locator('input[type="checkbox"]').nth(fileIndex);

    await fileCheckbox.check();
  }

  /**
   * Keep a specific file (mark others for deletion)
   */
  async keepFile(groupIndex: number, fileIndex: number): Promise<void> {
    await this.expandGroup(groupIndex);

    const group = this.groupItems.nth(groupIndex);
    const keepButtons = group.locator('button[aria-label*="keep" i]');

    await keepButtons.nth(fileIndex).click();
  }

  /**
   * Delete selected files
   */
  async deleteSelected(): Promise<void> {
    await this.deleteSelectedButton.click();

    // Confirm deletion
    const confirmButton = this.page.getByRole('button', { name: /confirm|yes|delete/i });
    const confirmVisible = await confirmButton.isVisible({ timeout: 2000 }).catch(() => false);

    if (confirmVisible) {
      await confirmButton.click();
    }

    await this.waitForPageLoad();
  }

  /**
   * Filter by minimum file size
   */
  async filterByMinSize(sizeMB: number): Promise<void> {
    await this.minSizeInput.fill(sizeMB.toString());
    await this.waitForPageLoad();
  }

  /**
   * Auto-select oldest files in each group for deletion
   */
  async autoSelectOldestFiles(): Promise<void> {
    const autoSelectVisible = await this.autoSelectOldest.isVisible().catch(() => false);
    if (autoSelectVisible) {
      await this.autoSelectOldest.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Auto-select smallest quality files in each group for deletion
   */
  async autoSelectSmallestFiles(): Promise<void> {
    const autoSelectVisible = await this.autoSelectSmallest.isVisible().catch(() => false);
    if (autoSelectVisible) {
      await this.autoSelectSmallest.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Get total duplicates count
   */
  async getTotalDuplicates(): Promise<string> {
    return await this.totalDuplicates.textContent() ?? '';
  }

  /**
   * Get potential savings value
   */
  async getPotentialSavings(): Promise<string> {
    return await this.potentialSavings.textContent() ?? '';
  }

  /**
   * Open compare view for a group
   */
  async openCompareView(groupIndex: number): Promise<void> {
    const group = this.groupItems.nth(groupIndex);
    const compareButton = group.getByRole('button', { name: /compare/i });

    const compareButtonVisible = await compareButton.isVisible().catch(() => false);
    if (compareButtonVisible) {
      await compareButton.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Assert that duplicate groups are visible
   */
  async expectGroupsVisible(): Promise<void> {
    await expect(this.duplicateGroups).toBeVisible();
  }

  /**
   * Assert that there are duplicate groups
   */
  async expectHasGroups(): Promise<void> {
    const count = await this.getGroupCount();
    expect(count).toBeGreaterThan(0);
  }

  /**
   * Check if image preview is visible
   */
  async hasImagePreview(): Promise<boolean> {
    return await this.imagePreview.isVisible().catch(() => false);
  }

  /**
   * Get the number of files in a specific group
   */
  async getFilesInGroup(groupIndex: number): Promise<number> {
    await this.expandGroup(groupIndex);

    const group = this.groupItems.nth(groupIndex);
    const files = group.locator('.file-item, [data-testid="file-item"]');

    return await files.count();
  }
}
