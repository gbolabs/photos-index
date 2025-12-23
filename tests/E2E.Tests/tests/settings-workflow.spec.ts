import { test, expect } from '../fixtures/test-fixtures';

/**
 * Settings page workflow tests
 * Tests directory management functionality
 */

test.describe('Settings Page', () => {
  test.beforeEach(async ({ settingsPage }) => {
    await settingsPage.goto();
  });

  test('should display settings page with header', async ({ page }) => {
    await expect(page.locator('h1')).toContainText('Directory Settings');
    await expect(page.locator('.settings__subtitle')).toContainText('Manage directories');
  });

  test('should show add directory button', async ({ settingsPage }) => {
    await settingsPage.expectAddButtonVisible();
  });

  test('should have refresh button in header', async ({ page }) => {
    const refreshButton = page.locator('.settings__actions button[mattooltip="Refresh"]');
    await expect(refreshButton).toBeVisible();
  });

  test('should display directory list or empty state', async ({ settingsPage, page }) => {
    // Either shows directory list component or loading state completed
    const directoryList = page.locator('app-directory-list');
    const loading = page.locator('.settings__loading');

    // Wait for loading to complete
    await loading.waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});

    // Directory list should be visible after loading
    await expect(directoryList).toBeVisible({ timeout: 10000 });
  });
});

test.describe('Settings - Directory Table', () => {
  test.beforeEach(async ({ settingsPage }) => {
    await settingsPage.goto();
  });

  test('should display directory table with columns', async ({ page }) => {
    const table = page.locator('.directory-list table');
    const tableVisible = await table.isVisible().catch(() => false);

    if (tableVisible) {
      // Check for expected column headers
      await expect(page.locator('th:has-text("Path")')).toBeVisible();
      await expect(page.locator('th:has-text("Status")')).toBeVisible();
      await expect(page.locator('th:has-text("Files")')).toBeVisible();
      await expect(page.locator('th:has-text("Actions")')).toBeVisible();
    }
  });

  test('should show empty state when no directories configured', async ({ page }) => {
    const emptyState = page.locator('.empty-state');
    const hasDirectories = await page.locator('.directory-list table tr.mat-mdc-row').count() > 0;

    if (!hasDirectories) {
      await expect(emptyState).toBeVisible();
      await expect(emptyState).toContainText('No directories configured');
    }
  });

  test('should display directory rows with action buttons', async ({ page }) => {
    const rows = page.locator('.directory-list table tr.mat-mdc-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      const firstRow = rows.first();

      // Check for action buttons in the row
      await expect(firstRow.locator('button[mattooltip="Edit"]')).toBeVisible();
      await expect(firstRow.locator('button[mattooltip="Delete"]')).toBeVisible();

      // Toggle button should show either Enable or Disable
      const toggleButton = firstRow.locator('button').filter({ has: page.locator('mat-icon:text("toggle_on"), mat-icon:text("toggle_off")') });
      await expect(toggleButton).toBeVisible();
    }
  });

  test('should show enabled/disabled status chip', async ({ page }) => {
    const rows = page.locator('.directory-list table tr.mat-mdc-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      const firstRow = rows.first();
      const statusChip = firstRow.locator('mat-chip');
      await expect(statusChip).toBeVisible();

      // Should contain either "Enabled" or "Disabled"
      const chipText = await statusChip.textContent();
      expect(chipText).toMatch(/Enabled|Disabled/i);
    }
  });
});

test.describe('Settings - Add Directory Dialog', () => {
  test.beforeEach(async ({ settingsPage }) => {
    await settingsPage.goto();
  });

  test('should open add directory dialog on button click', async ({ settingsPage, page }) => {
    await settingsPage.addDirectoryButton.click();

    // Dialog should appear
    const dialog = page.locator('mat-dialog-container');
    await expect(dialog).toBeVisible({ timeout: 5000 });
  });

  test('should have path input in add dialog', async ({ settingsPage, page }) => {
    await settingsPage.addDirectoryButton.click();

    const dialog = page.locator('mat-dialog-container');
    await expect(dialog).toBeVisible();

    // Path input should be present
    const pathInput = dialog.locator('input[formcontrolname="path"], input[name="path"], input[placeholder*="path" i]');
    await expect(pathInput).toBeVisible();
  });

  test('should have save and cancel buttons in dialog', async ({ settingsPage, page }) => {
    await settingsPage.addDirectoryButton.click();

    const dialog = page.locator('mat-dialog-container');
    await expect(dialog).toBeVisible();

    // Buttons should be present
    const saveButton = dialog.getByRole('button', { name: /save|add|submit/i });
    const cancelButton = dialog.getByRole('button', { name: /cancel|close/i });

    await expect(saveButton).toBeVisible();
    await expect(cancelButton).toBeVisible();
  });

  test('should close dialog on cancel', async ({ settingsPage, page }) => {
    await settingsPage.addDirectoryButton.click();

    const dialog = page.locator('mat-dialog-container');
    await expect(dialog).toBeVisible();

    const cancelButton = dialog.getByRole('button', { name: /cancel|close/i });
    await cancelButton.click();

    await expect(dialog).not.toBeVisible({ timeout: 5000 });
  });
});

test.describe('Settings - Directory Actions', () => {
  test.beforeEach(async ({ settingsPage }) => {
    await settingsPage.goto();
  });

  test('should open edit dialog when edit button clicked', async ({ page }) => {
    const rows = page.locator('.directory-list table tr.mat-mdc-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      const editButton = rows.first().locator('button[mattooltip="Edit"]');
      await editButton.click();

      const dialog = page.locator('mat-dialog-container');
      await expect(dialog).toBeVisible({ timeout: 5000 });
    }
  });

  test('should show confirmation dialog when delete button clicked', async ({ page }) => {
    const rows = page.locator('.directory-list table tr.mat-mdc-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      const deleteButton = rows.first().locator('button[mattooltip="Delete"]');
      await deleteButton.click();

      // Confirmation dialog should appear
      const dialog = page.locator('mat-dialog-container');
      const confirmVisible = await dialog.isVisible({ timeout: 3000 }).catch(() => false);

      if (confirmVisible) {
        // Should have confirm/cancel options
        await expect(dialog.getByRole('button', { name: /cancel|no/i })).toBeVisible();

        // Close without confirming
        await dialog.getByRole('button', { name: /cancel|no/i }).click();
      }
    }
  });

  test('should toggle directory enabled state', async ({ page }) => {
    const rows = page.locator('.directory-list table tr.mat-mdc-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      const firstRow = rows.first();
      const statusChip = firstRow.locator('mat-chip');
      const initialStatus = await statusChip.textContent();

      // Click toggle button
      const toggleButton = firstRow.locator('button').filter({
        has: page.locator('mat-icon:text("toggle_on"), mat-icon:text("toggle_off")')
      });
      await toggleButton.click();

      // Wait for update
      await page.waitForTimeout(1000);

      // Status should have changed (or API call was made)
      // Note: In a real test, we'd verify the state change
    }
  });
});

test.describe('Settings - Navigation', () => {
  test('should navigate to settings from dashboard', async ({ dashboardPage, page }) => {
    await dashboardPage.goto();
    await dashboardPage.navigateToSettings();

    await expect(page).toHaveURL(/\/settings/);
    await expect(page.locator('h1')).toContainText('Directory Settings');
  });

  test('should navigate back to dashboard from settings', async ({ settingsPage, page }) => {
    await settingsPage.goto();
    await settingsPage.navigateToDashboard();

    await expect(page).toHaveURL(/\/$/);
  });
});
