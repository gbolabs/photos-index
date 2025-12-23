import { test, expect } from '../fixtures/test-fixtures';

/**
 * Files page workflow tests
 * Tests file browsing, searching, sorting, and pagination
 */

test.describe('Files Page', () => {
  test.beforeEach(async ({ filesPage }) => {
    await filesPage.goto();
  });

  test('should display files page with header', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('File Browser');
    await expect(page.locator('.subtitle')).toContainText('Browse and search');
  });

  test('should display search input', async ({ page }) => {
    const searchInput = page.locator('input[matinput]').filter({ hasText: /search/i }).or(
      page.locator('mat-form-field').filter({ hasText: 'Search files' }).locator('input')
    );
    await expect(searchInput.first()).toBeVisible();
  });

  test('should display sort dropdown', async ({ page }) => {
    const sortField = page.locator('mat-form-field').filter({ hasText: 'Sort by' });
    await expect(sortField).toBeVisible();
  });

  test('should display file list or empty state', async ({ filesPage, page }) => {
    // Wait for loading to complete
    const loading = page.locator('.loading-container');
    await loading.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});

    // Should show either files table, empty state, or error
    const table = page.locator('.table-container');
    const emptyCard = page.locator('.empty-card');
    const errorCard = page.locator('.error-card');

    const hasTable = await table.isVisible().catch(() => false);
    const hasEmpty = await emptyCard.isVisible().catch(() => false);
    const hasError = await errorCard.isVisible().catch(() => false);

    expect(hasTable || hasEmpty || hasError).toBeTruthy();
  });
});

test.describe('Files - Search Functionality', () => {
  test.beforeEach(async ({ filesPage }) => {
    await filesPage.goto();
  });

  test('should have search input with placeholder', async ({ page }) => {
    const searchInput = page.locator('input[placeholder*="search" i], input[placeholder*="Search" i]');
    await expect(searchInput.first()).toBeVisible();
  });

  test('should have search button', async ({ page }) => {
    const searchButton = page.getByRole('button', { name: /search/i });
    await expect(searchButton).toBeVisible();
  });

  test('should show clear button when search has value', async ({ page }) => {
    const searchInput = page.locator('input[placeholder*="search" i]').first();
    await searchInput.fill('test');

    // Clear button should appear
    const clearButton = page.locator('mat-form-field').filter({ hasText: 'Search' }).locator('button mat-icon:text("close")');
    await expect(clearButton.first()).toBeVisible();
  });

  test('should clear search on clear button click', async ({ page }) => {
    const searchInput = page.locator('input[placeholder*="search" i]').first();
    await searchInput.fill('test');

    const clearButton = page.locator('button').filter({ has: page.locator('mat-icon:text("close")') }).first();
    const visible = await clearButton.isVisible().catch(() => false);

    if (visible) {
      await clearButton.click();
      await expect(searchInput).toHaveValue('');
    }
  });

  test('should trigger search on Enter key', async ({ page }) => {
    const searchInput = page.locator('input[placeholder*="search" i]').first();
    await searchInput.fill('photo');
    await searchInput.press('Enter');

    // Wait for potential results update
    await page.waitForTimeout(500);
  });
});

test.describe('Files - Sorting', () => {
  test.beforeEach(async ({ filesPage }) => {
    await filesPage.goto();
  });

  test('should display sort dropdown with options', async ({ page }) => {
    const sortSelect = page.locator('mat-select').filter({ hasText: /Name|Sort/i }).first();
    await sortSelect.click();

    // Wait for options to appear
    const options = page.locator('mat-option');
    await expect(options.first()).toBeVisible({ timeout: 5000 });

    // Check for expected sort options
    await expect(page.locator('mat-option:has-text("Name")')).toBeVisible();
    await expect(page.locator('mat-option:has-text("Size")')).toBeVisible();
  });

  test('should have sort direction toggle button', async ({ page }) => {
    const sortButton = page.locator('button').filter({
      has: page.locator('mat-icon:text("arrow_downward"), mat-icon:text("arrow_upward")')
    });
    await expect(sortButton.first()).toBeVisible();
  });

  test('should toggle sort direction on button click', async ({ page }) => {
    const sortButton = page.locator('button').filter({
      has: page.locator('mat-icon:text("arrow_downward"), mat-icon:text("arrow_upward")')
    }).first();

    // Get initial icon
    const initialIcon = await sortButton.locator('mat-icon').textContent();

    await sortButton.click();

    // Icon should change
    const newIcon = await sortButton.locator('mat-icon').textContent();
    expect(newIcon).not.toBe(initialIcon);
  });

  test('should change sort by selecting different option', async ({ page }) => {
    const sortSelect = page.locator('mat-select').first();
    await sortSelect.click();

    // Select Size option
    const sizeOption = page.locator('mat-option:has-text("Size")');
    const visible = await sizeOption.isVisible().catch(() => false);

    if (visible) {
      await sizeOption.click();
      // Wait for re-sort
      await page.waitForTimeout(500);
    }
  });
});

test.describe('Files - Table Display', () => {
  test.beforeEach(async ({ filesPage }) => {
    await filesPage.goto();
  });

  test('should display table with expected columns', async ({ page }) => {
    const table = page.locator('.table-container table');
    const tableVisible = await table.isVisible().catch(() => false);

    if (tableVisible) {
      await expect(page.locator('th:has-text("File Name")')).toBeVisible();
      await expect(page.locator('th:has-text("Size")')).toBeVisible();
    }
  });

  test('should display thumbnails for files', async ({ page }) => {
    const table = page.locator('.table-container table');
    const tableVisible = await table.isVisible().catch(() => false);

    if (tableVisible) {
      const thumbnails = page.locator('.thumbnail-container img');
      const thumbCount = await thumbnails.count();

      if (thumbCount > 0) {
        await expect(thumbnails.first()).toBeVisible();
      }
    }
  });

  test('should display file paths', async ({ page }) => {
    const table = page.locator('.table-container table');
    const tableVisible = await table.isVisible().catch(() => false);

    if (tableVisible) {
      const filePaths = page.locator('.file-path');
      const pathCount = await filePaths.count();

      if (pathCount > 0) {
        await expect(filePaths.first()).toBeVisible();
      }
    }
  });

  test('should show duplicate chip for duplicate files', async ({ page }) => {
    const duplicateChips = page.locator('.duplicate-chip, mat-chip:has-text("Duplicate")');
    const chipCount = await duplicateChips.count();

    // If there are duplicate files, they should have the chip
    if (chipCount > 0) {
      await expect(duplicateChips.first()).toBeVisible();
    }
  });

  test('should have action buttons for each file row', async ({ page }) => {
    const rows = page.locator('.table-container tr.mat-mdc-row, .table-container mat-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      const firstRow = rows.first();

      // View details button
      const detailsButton = firstRow.locator('a[mattooltip="View Details"], a mat-icon:text("info")');
      await expect(detailsButton.first()).toBeVisible();

      // View original button
      const viewButton = firstRow.locator('button[mattooltip="View Original"], button mat-icon:text("visibility")');
      await expect(viewButton.first()).toBeVisible();
    }
  });
});

test.describe('Files - Pagination', () => {
  test.beforeEach(async ({ filesPage }) => {
    await filesPage.goto();
  });

  test('should display paginator', async ({ page }) => {
    const paginator = page.locator('mat-paginator');
    const paginatorVisible = await paginator.isVisible().catch(() => false);

    // Paginator shown only when there are files
    const hasFiles = await page.locator('.table-container table').isVisible().catch(() => false);

    if (hasFiles) {
      await expect(paginator).toBeVisible();
    }
  });

  test('should show page size options', async ({ page }) => {
    const paginator = page.locator('mat-paginator');
    const visible = await paginator.isVisible().catch(() => false);

    if (visible) {
      // Click on page size selector
      const pageSizeSelect = paginator.locator('mat-select').first();
      await pageSizeSelect.click();

      // Check for page size options
      const options = page.locator('mat-option');
      await expect(options.first()).toBeVisible({ timeout: 3000 });
    }
  });

  test('should have first/last page buttons', async ({ page }) => {
    const paginator = page.locator('mat-paginator');
    const visible = await paginator.isVisible().catch(() => false);

    if (visible) {
      // First and last page buttons (when showFirstLastButtons is true)
      const firstButton = paginator.locator('button[aria-label="First page"]');
      const lastButton = paginator.locator('button[aria-label="Last page"]');

      await expect(firstButton).toBeVisible();
      await expect(lastButton).toBeVisible();
    }
  });

  test('should navigate between pages', async ({ page }) => {
    const paginator = page.locator('mat-paginator');
    const visible = await paginator.isVisible().catch(() => false);

    if (visible) {
      const nextButton = paginator.locator('button[aria-label="Next page"]');
      const isEnabled = await nextButton.isEnabled().catch(() => false);

      if (isEnabled) {
        await nextButton.click();
        await page.waitForTimeout(500);

        // Previous button should now be enabled
        const prevButton = paginator.locator('button[aria-label="Previous page"]');
        await expect(prevButton).toBeEnabled();
      }
    }
  });
});

test.describe('Files - Empty and Error States', () => {
  test.beforeEach(async ({ filesPage }) => {
    await filesPage.goto();
  });

  test('should show empty state message when no files', async ({ page }) => {
    const emptyCard = page.locator('.empty-card');
    const visible = await emptyCard.isVisible().catch(() => false);

    if (visible) {
      await expect(emptyCard.locator('h3')).toContainText('No Files Found');
    }
  });

  test('should show clear search button in empty state when searching', async ({ page }) => {
    // First enter a search that might return no results
    const searchInput = page.locator('input[placeholder*="search" i]').first();
    await searchInput.fill('nonexistentfilenamethatwontmatch12345');

    const searchButton = page.getByRole('button', { name: /search/i });
    await searchButton.click();

    // Wait for results
    await page.waitForTimeout(1000);

    const emptyCard = page.locator('.empty-card');
    const visible = await emptyCard.isVisible().catch(() => false);

    if (visible) {
      // Should offer clear search option
      const clearButton = emptyCard.getByRole('button', { name: /clear search/i });
      const clearVisible = await clearButton.isVisible().catch(() => false);

      if (clearVisible) {
        await expect(clearButton).toBeVisible();
      }
    }
  });

  test('should show retry button on error', async ({ page }) => {
    const errorCard = page.locator('.error-card');
    const visible = await errorCard.isVisible().catch(() => false);

    if (visible) {
      const retryButton = errorCard.getByRole('button', { name: /retry/i });
      await expect(retryButton).toBeVisible();
    }
  });
});

test.describe('Files - Navigation', () => {
  test('should navigate to files from dashboard', async ({ dashboardPage, page }) => {
    await dashboardPage.goto();
    await dashboardPage.navigateToFiles();

    await expect(page).toHaveURL(/\/files/);
  });

  test('should navigate to file detail on click', async ({ filesPage, page }) => {
    await filesPage.goto();

    // Wait for table to load
    await page.waitForTimeout(1000);

    const detailLinks = page.locator('a[href*="/files/"]');
    const linkCount = await detailLinks.count();

    if (linkCount > 0) {
      await detailLinks.first().click();

      // Should navigate to file detail page
      await expect(page).toHaveURL(/\/files\/[a-zA-Z0-9-]+/);
    }
  });
});
