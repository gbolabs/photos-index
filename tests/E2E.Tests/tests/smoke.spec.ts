import { test, expect } from '../fixtures/test-fixtures';

/**
 * Smoke tests for PhotosIndex application
 * These tests verify that core functionality is working
 */

test.describe('Smoke Tests', () => {
  test('dashboard loads successfully', async ({ dashboardPage }) => {
    await dashboardPage.goto();
    await dashboardPage.expectStatsVisible();
  });

  test('dashboard displays all statistics cards', async ({ dashboardPage }) => {
    await dashboardPage.goto();

    // Verify all stat cards are present
    await expect(dashboardPage.totalFilesCard).toBeVisible();
    await expect(dashboardPage.duplicatesCard).toBeVisible();
    await expect(dashboardPage.savingsCard).toBeVisible();
    await expect(dashboardPage.directoriesCard).toBeVisible();
  });

  test('navigation works between pages', async ({ dashboardPage, page }) => {
    await dashboardPage.goto();

    // Navigate to Settings
    await dashboardPage.navigateToSettings();
    await expect(page).toHaveURL(/\/settings/);

    // Navigate to Files
    await dashboardPage.navigateToFiles();
    await expect(page).toHaveURL(/\/files/);

    // Navigate to Duplicates
    await dashboardPage.navigateToDuplicates();
    await expect(page).toHaveURL(/\/duplicates/);

    // Navigate back to Dashboard
    await dashboardPage.navigateToDashboard();
    await expect(page).toHaveURL(/\/$/);
  });

  test('settings page loads successfully', async ({ settingsPage }) => {
    await settingsPage.goto();
    await settingsPage.expectAddButtonVisible();
  });

  test('files page loads successfully', async ({ filesPage }) => {
    await filesPage.goto();
    await filesPage.expectFileListVisible();
  });

  test('duplicates page loads successfully', async ({ duplicatesPage }) => {
    await duplicatesPage.goto();
    await duplicatesPage.expectGroupsVisible();
  });

  test('app is responsive on mobile viewport', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });

    // Navigate to dashboard
    await page.goto('/');

    // Verify page loads and is visible
    await page.waitForLoadState('networkidle');
    const body = page.locator('body');
    await expect(body).toBeVisible();

    // Verify navigation menu is accessible (may be hamburger menu on mobile)
    const nav = page.locator('nav, .nav, mat-sidenav, mat-toolbar');
    const navCount = await nav.count();
    expect(navCount).toBeGreaterThan(0);
  });

  test('app is responsive on tablet viewport', async ({ page }) => {
    // Set tablet viewport
    await page.setViewportSize({ width: 768, height: 1024 });

    // Navigate to dashboard
    await page.goto('/');

    // Verify page loads
    await page.waitForLoadState('networkidle');
    const body = page.locator('body');
    await expect(body).toBeVisible();
  });

  test('page titles are correct', async ({ page }) => {
    // Dashboard
    await page.goto('/');
    await expect(page).toHaveTitle(/Photos\s*Index|Dashboard/i);

    // Settings
    await page.goto('/settings');
    await expect(page).toHaveTitle(/Settings|Photos\s*Index/i);

    // Files
    await page.goto('/files');
    await expect(page).toHaveTitle(/Files|Photos\s*Index/i);

    // Duplicates
    await page.goto('/duplicates');
    await expect(page).toHaveTitle(/Duplicates|Photos\s*Index/i);
  });

  test('refresh button works on dashboard', async ({ dashboardPage }) => {
    await dashboardPage.goto();

    // Click refresh button if visible
    const refreshVisible = await dashboardPage.refreshButton.isVisible().catch(() => false);
    if (refreshVisible) {
      await dashboardPage.refresh();
      // Verify page is still functional after refresh
      await dashboardPage.expectStatsVisible();
    }
  });

  test('no JavaScript errors on page load', async ({ page }) => {
    const errors: string[] = [];

    // Listen for console errors
    page.on('pageerror', (error) => {
      errors.push(error.message);
    });

    // Navigate to each page
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    await page.goto('/files');
    await page.waitForLoadState('networkidle');

    await page.goto('/duplicates');
    await page.waitForLoadState('networkidle');

    // No critical errors should be present
    const criticalErrors = errors.filter(err =>
      !err.includes('favicon') && // Ignore favicon errors
      !err.includes('ECONNREFUSED') // Ignore connection errors in dev
    );

    expect(criticalErrors.length).toBe(0);
  });
});

test.describe('Smoke Tests - Mobile Viewports', () => {
  test('dashboard works on Pixel 5', async ({ page }) => {
    await page.setViewportSize({ width: 393, height: 851 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('body')).toBeVisible();
  });

  test('dashboard works on iPhone 12', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('body')).toBeVisible();
  });
});
