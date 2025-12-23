import { test, expect } from '../fixtures/test-fixtures';

/**
 * Indexing page workflow tests
 * Tests scanning operations and status monitoring
 */

test.describe('Indexing Page', () => {
  test.beforeEach(async ({ indexingPage }) => {
    await indexingPage.goto();
  });

  test('should display indexing page with header', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Indexing Status');
    await expect(page.locator('.subtitle')).toContainText('Monitor and control');
  });

  test('should display scan all button', async ({ indexingPage }) => {
    await expect(indexingPage.scanAllButton).toBeVisible();
  });

  test('should display refresh button', async ({ page }) => {
    const refreshButton = page.locator('.header-actions button[mattooltip="Refresh"]');
    await expect(refreshButton).toBeVisible();
  });

  test('should display summary cards or loading state', async ({ indexingPage, page }) => {
    // Wait for loading to complete
    const loading = page.locator('.loading-container');
    await loading.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});

    // Should show summary cards or error
    const summaryCards = page.locator('.summary-cards');
    const errorCard = page.locator('.error-card');

    const hasSummary = await summaryCards.isVisible().catch(() => false);
    const hasError = await errorCard.isVisible().catch(() => false);

    expect(hasSummary || hasError).toBeTruthy();
  });
});

test.describe('Indexing - Summary Cards', () => {
  test.beforeEach(async ({ indexingPage }) => {
    await indexingPage.goto();
  });

  test('should display total directories card', async ({ indexingPage }) => {
    await indexingPage.expectSummaryVisible();
  });

  test('should show directory count in summary', async ({ indexingPage, page }) => {
    // Wait for data to load
    await page.waitForTimeout(1000);

    const summaryCard = page.locator('.summary-card:has-text("Total Directories")');
    const visible = await summaryCard.isVisible().catch(() => false);

    if (visible) {
      const value = summaryCard.locator('.summary-value');
      await expect(value).toBeVisible();

      // Value should be a number
      const text = await value.textContent();
      expect(text).toMatch(/^\d+$/);
    }
  });

  test('should show enabled directories count', async ({ page }) => {
    const enabledCard = page.locator('.summary-card:has-text("Enabled")');
    const visible = await enabledCard.isVisible().catch(() => false);

    if (visible) {
      const value = enabledCard.locator('.summary-value');
      await expect(value).toBeVisible();
    }
  });

  test('should show total files count', async ({ page }) => {
    const filesCard = page.locator('.summary-card:has-text("Total Files")');
    const visible = await filesCard.isVisible().catch(() => false);

    if (visible) {
      const value = filesCard.locator('.summary-value');
      await expect(value).toBeVisible();
    }
  });
});

test.describe('Indexing - Directory List', () => {
  test.beforeEach(async ({ indexingPage }) => {
    await indexingPage.goto();
  });

  test('should display directory cards or empty state', async ({ indexingPage, page }) => {
    // Wait for loading
    await page.waitForTimeout(1000);

    const dirCards = page.locator('.directory-card');
    const emptyCard = page.locator('.empty-card');

    const hasDirs = await dirCards.count() > 0;
    const hasEmpty = await emptyCard.isVisible().catch(() => false);

    expect(hasDirs || hasEmpty).toBeTruthy();
  });

  test('should show directory path in card', async ({ page }) => {
    const dirCards = page.locator('.directory-card');
    const count = await dirCards.count();

    if (count > 0) {
      const pathElement = dirCards.first().locator('.directory-path');
      await expect(pathElement).toBeVisible();

      const path = await pathElement.textContent();
      expect(path?.length).toBeGreaterThan(0);
    }
  });

  test('should show enabled/disabled chip on directory card', async ({ page }) => {
    const dirCards = page.locator('.directory-card');
    const count = await dirCards.count();

    if (count > 0) {
      const chip = dirCards.first().locator('mat-chip');
      await expect(chip).toBeVisible();

      const chipText = await chip.textContent();
      expect(chipText).toMatch(/Enabled|Disabled/i);
    }
  });

  test('should show file count for each directory', async ({ page }) => {
    const dirCards = page.locator('.directory-card');
    const count = await dirCards.count();

    if (count > 0) {
      const fileCount = dirCards.first().locator('.file-count');
      await expect(fileCount).toBeVisible();

      const text = await fileCount.textContent();
      expect(text).toContain('files');
    }
  });

  test('should show scan button for enabled directories', async ({ page }) => {
    const enabledDirs = page.locator('.directory-card:not(.disabled)');
    const count = await enabledDirs.count();

    if (count > 0) {
      const scanButton = enabledDirs.first().locator('button[mattooltip="Scan this directory"]');
      await expect(scanButton).toBeVisible();
      await expect(scanButton).toBeEnabled();
    }
  });

  test('should disable scan button for disabled directories', async ({ page }) => {
    const disabledDirs = page.locator('.directory-card.disabled');
    const count = await disabledDirs.count();

    if (count > 0) {
      const scanButton = disabledDirs.first().locator('button[mattooltip="Scan this directory"]');
      await expect(scanButton).toBeDisabled();
    }
  });

  test('should show last scanned date when available', async ({ page }) => {
    const dirCards = page.locator('.directory-card');
    const count = await dirCards.count();

    if (count > 0) {
      // Check for last scanned info in footer
      const footer = dirCards.first().locator('.directory-footer');
      const hasFooter = await footer.isVisible().catch(() => false);

      if (hasFooter) {
        await expect(footer).toContainText('Last scanned');
      }
    }
  });
});

test.describe('Indexing - Empty State', () => {
  test('should show go to settings link when no directories', async ({ page, indexingPage }) => {
    await indexingPage.goto();

    const emptyCard = page.locator('.empty-card');
    const visible = await emptyCard.isVisible().catch(() => false);

    if (visible) {
      await expect(emptyCard.locator('h3')).toContainText('No Directories Configured');

      const settingsLink = emptyCard.locator('a[routerlink="/settings"]');
      await expect(settingsLink).toBeVisible();
      await expect(settingsLink).toContainText('Go to Settings');
    }
  });

  test('should navigate to settings from empty state', async ({ page, indexingPage }) => {
    await indexingPage.goto();

    const settingsLink = page.locator('.empty-card a[routerlink="/settings"]');
    const visible = await settingsLink.isVisible().catch(() => false);

    if (visible) {
      await settingsLink.click();
      await expect(page).toHaveURL(/\/settings/);
    }
  });
});

test.describe('Indexing - Scan Operations', () => {
  test.beforeEach(async ({ indexingPage }) => {
    await indexingPage.goto();
  });

  test('should have scan all button enabled when directories exist', async ({ page, indexingPage }) => {
    const dirCards = page.locator('.directory-card:not(.disabled)');
    const enabledCount = await dirCards.count();

    if (enabledCount > 0) {
      await expect(indexingPage.scanAllButton).toBeEnabled();
    } else {
      await expect(indexingPage.scanAllButton).toBeDisabled();
    }
  });

  test('should show progress bar when scan is running', async ({ page }) => {
    // This test checks for the presence of status card when indexing is active
    const statusCard = page.locator('.indexing-status-card');
    const isRunning = await statusCard.isVisible().catch(() => false);

    if (isRunning) {
      const progressBar = statusCard.locator('mat-progress-bar');
      await expect(progressBar).toBeVisible();
    }
  });

  test('should show scanning stats when indexing', async ({ page }) => {
    const statusCard = page.locator('.indexing-status-card');
    const isRunning = await statusCard.isVisible().catch(() => false);

    if (isRunning) {
      // Check for stats
      await expect(page.locator('.stat:has-text("Scanned")')).toBeVisible();
      await expect(page.locator('.stat:has-text("Ingested")')).toBeVisible();
      await expect(page.locator('.stat:has-text("Failed")')).toBeVisible();
      await expect(page.locator('.stat:has-text("Progress")')).toBeVisible();
    }
  });

  test('should show elapsed time when indexing', async ({ page }) => {
    const statusCard = page.locator('.indexing-status-card');
    const isRunning = await statusCard.isVisible().catch(() => false);

    if (isRunning) {
      const elapsed = page.locator('.status-elapsed');
      await expect(elapsed).toBeVisible();
    }
  });

  test('should show scanning indicator on directory card', async ({ page }) => {
    const scanningCards = page.locator('.directory-card.scanning');
    const count = await scanningCards.count();

    if (count > 0) {
      // Should have progress bar
      const progressBar = scanningCards.first().locator('mat-progress-bar');
      await expect(progressBar).toBeVisible();

      // Should show spinner instead of play icon
      const spinner = scanningCards.first().locator('mat-spinner');
      await expect(spinner).toBeVisible();
    }
  });
});

test.describe('Indexing - Auto Refresh', () => {
  test.beforeEach(async ({ indexingPage }) => {
    await indexingPage.goto();
  });

  test('should display auto-refresh indicator', async ({ indexingPage }) => {
    await indexingPage.expectAutoRefreshVisible();
  });

  test('should show correct refresh message', async ({ page }) => {
    const indicator = page.locator('.auto-refresh-indicator');
    await expect(indicator).toContainText('Auto-refreshing every 10 seconds');
  });
});

test.describe('Indexing - Navigation', () => {
  test('should navigate to indexing from drawer', async ({ dashboardPage, page }) => {
    await dashboardPage.goto();

    // Open drawer and click indexing
    await dashboardPage.openDrawer();
    const indexingLink = page.locator('mat-nav-list a').filter({ hasText: 'Indexing' });
    await indexingLink.click();

    await expect(page).toHaveURL(/\/indexing/);
  });

  test('should navigate back to dashboard', async ({ indexingPage, page }) => {
    await indexingPage.goto();
    await indexingPage.navigateToDashboard();

    await expect(page).toHaveURL(/\/$/);
  });
});
