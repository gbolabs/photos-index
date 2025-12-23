import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './base.page';

/**
 * Page object for the Files page
 * Browse and search indexed files
 */
export class FilesPage extends BasePage {
  readonly url = '/files';

  // Search and filters
  readonly searchInput: Locator;
  readonly searchButton: Locator;
  readonly clearSearchButton: Locator;
  readonly filterButton: Locator;
  readonly sortDropdown: Locator;

  // File list/grid
  readonly fileList: Locator;
  readonly fileItems: Locator;
  readonly fileGrid: Locator;

  // View mode toggles
  readonly listViewButton: Locator;
  readonly gridViewButton: Locator;

  // Pagination
  readonly pagination: Locator;
  readonly nextPageButton: Locator;
  readonly prevPageButton: Locator;
  readonly pageInfo: Locator;

  // File details
  readonly filePreview: Locator;
  readonly fileMetadata: Locator;

  // Actions
  readonly selectAllCheckbox: Locator;
  readonly bulkDeleteButton: Locator;

  constructor(page: Page) {
    super(page);

    // Search and filters
    this.searchInput = page.locator('input[type="search"], input[placeholder*="search" i]');
    this.searchButton = page.getByRole('button', { name: /search/i });
    this.clearSearchButton = page.getByRole('button', { name: /clear/i });
    this.filterButton = page.getByRole('button', { name: /filter/i });
    this.sortDropdown = page.locator('mat-select, select[name="sort"]');

    // File list/grid - also match Angular Material table structure
    this.fileList = page.locator('.file-list, .table-container, .files-container mat-table, [data-testid="file-list"]');
    this.fileItems = page.locator('.file-item, tr.mat-mdc-row, mat-row, [data-testid="file-item"]');
    this.fileGrid = page.locator('.file-grid, .files-container, [data-testid="file-grid"]');

    // View mode
    this.listViewButton = page.getByRole('button', { name: /list view/i });
    this.gridViewButton = page.getByRole('button', { name: /grid view/i });

    // Pagination
    this.pagination = page.locator('mat-paginator, .pagination');
    this.nextPageButton = page.getByRole('button', { name: /next/i });
    this.prevPageButton = page.getByRole('button', { name: /previous/i });
    this.pageInfo = page.locator('.page-info, [data-testid="page-info"]');

    // File details
    this.filePreview = page.locator('.file-preview, [data-testid="file-preview"]');
    this.fileMetadata = page.locator('.file-metadata, [data-testid="file-metadata"]');

    // Bulk actions
    this.selectAllCheckbox = page.locator('input[type="checkbox"][aria-label*="select all" i]');
    this.bulkDeleteButton = page.getByRole('button', { name: /delete selected/i });
  }

  /**
   * Navigate to the files page
   */
  async goto(): Promise<void> {
    await this.page.goto(this.url);
    await this.waitForPageLoad();
  }

  /**
   * Search for files by query
   */
  async search(query: string): Promise<void> {
    await this.searchInput.fill(query);
    await this.searchButton.click();
    await this.waitForPageLoad();
  }

  /**
   * Clear the search
   */
  async clearSearch(): Promise<void> {
    const clearButtonVisible = await this.clearSearchButton.isVisible().catch(() => false);
    if (clearButtonVisible) {
      await this.clearSearchButton.click();
      await this.waitForPageLoad();
    } else {
      await this.searchInput.clear();
      await this.searchButton.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Sort files by criteria
   */
  async sortBy(criteria: string): Promise<void> {
    await this.sortDropdown.click();
    await this.page.getByRole('option', { name: new RegExp(criteria, 'i') }).click();
    await this.waitForPageLoad();
  }

  /**
   * Switch to list view
   */
  async switchToListView(): Promise<void> {
    const listButtonVisible = await this.listViewButton.isVisible().catch(() => false);
    if (listButtonVisible) {
      await this.listViewButton.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Switch to grid view
   */
  async switchToGridView(): Promise<void> {
    const gridButtonVisible = await this.gridViewButton.isVisible().catch(() => false);
    if (gridButtonVisible) {
      await this.gridViewButton.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Get the number of files displayed
   */
  async getFileCount(): Promise<number> {
    return await this.fileItems.count();
  }

  /**
   * Click on a file by index
   */
  async clickFile(index: number): Promise<void> {
    await this.fileItems.nth(index).click();
    await this.waitForPageLoad();
  }

  /**
   * Click on a file by filename
   */
  async clickFileByName(filename: string): Promise<void> {
    const fileItem = this.fileItems.filter({ hasText: filename });
    await fileItem.click();
    await this.waitForPageLoad();
  }

  /**
   * Go to the next page
   */
  async nextPage(): Promise<void> {
    const nextButtonEnabled = await this.nextPageButton.isEnabled().catch(() => false);
    if (nextButtonEnabled) {
      await this.nextPageButton.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Go to the previous page
   */
  async prevPage(): Promise<void> {
    const prevButtonEnabled = await this.prevPageButton.isEnabled().catch(() => false);
    if (prevButtonEnabled) {
      await this.prevPageButton.click();
      await this.waitForPageLoad();
    }
  }

  /**
   * Get current page information
   */
  async getPageInfo(): Promise<string> {
    return await this.pageInfo.textContent() ?? '';
  }

  /**
   * Select all files
   */
  async selectAll(): Promise<void> {
    const selectAllVisible = await this.selectAllCheckbox.isVisible().catch(() => false);
    if (selectAllVisible) {
      await this.selectAllCheckbox.check();
    }
  }

  /**
   * Select a file by index
   */
  async selectFile(index: number): Promise<void> {
    const checkbox = this.fileItems.nth(index).locator('input[type="checkbox"]');
    await checkbox.check();
  }

  /**
   * Delete selected files
   */
  async deleteSelected(): Promise<void> {
    await this.bulkDeleteButton.click();

    // Confirm deletion if dialog appears
    const confirmButton = this.page.getByRole('button', { name: /confirm|yes|delete/i });
    const confirmVisible = await confirmButton.isVisible({ timeout: 2000 }).catch(() => false);

    if (confirmVisible) {
      await confirmButton.click();
    }

    await this.waitForPageLoad();
  }

  /**
   * Assert that the file list is visible
   */
  async expectFileListVisible(): Promise<void> {
    const listVisible = await this.fileList.isVisible().catch(() => false);
    const gridVisible = await this.fileGrid.isVisible().catch(() => false);

    expect(listVisible || gridVisible).toBeTruthy();
  }

  /**
   * Check if file preview is visible
   */
  async hasFilePreview(): Promise<boolean> {
    return await this.filePreview.isVisible().catch(() => false);
  }

  /**
   * Get file metadata
   */
  async getFileMetadata(): Promise<string> {
    return await this.fileMetadata.textContent() ?? '';
  }
}
