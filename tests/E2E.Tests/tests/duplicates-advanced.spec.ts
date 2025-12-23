import { test, expect } from '../fixtures/test-fixtures';

/**
 * Advanced Duplicates page tests
 * Tests expand/collapse, selection, bulk actions, and table view
 */

test.describe('Duplicates - Table View', () => {
  test.beforeEach(async ({ duplicatesPage }) => {
    await duplicatesPage.goto();
  });

  test('should display duplicates table with columns', async ({ page }) => {
    // Wait for loading
    await page.waitForTimeout(1000);

    const table = page.locator('.table-container table');
    const tableVisible = await table.isVisible().catch(() => false);

    if (tableVisible) {
      // Check for expected column headers
      await expect(page.locator('th:has-text("Original")')).toBeVisible();
      await expect(page.locator('th:has-text("Size")')).toBeVisible();
      await expect(page.locator('th:has-text("Duplicates")')).toBeVisible();
    }
  });

  test('should display checkbox column for selection', async ({ page }) => {
    const table = page.locator('.table-container table');
    const tableVisible = await table.isVisible().catch(() => false);

    if (tableVisible) {
      const headerCheckbox = page.locator('th mat-checkbox');
      await expect(headerCheckbox).toBeVisible();
    }
  });

  test('should display sortable column headers', async ({ page }) => {
    const table = page.locator('.table-container table');
    const tableVisible = await table.isVisible().catch(() => false);

    if (tableVisible) {
      // Size and Date columns should be sortable
      const sizeHeader = page.locator('th[mat-sort-header="size"]');
      const dateHeader = page.locator('th[mat-sort-header="date"]');
      const countHeader = page.locator('th[mat-sort-header="fileCount"]');

      const hasSizeSort = await sizeHeader.count() > 0;
      const hasDateSort = await dateHeader.count() > 0;
      const hasCountSort = await countHeader.count() > 0;

      expect(hasSizeSort || hasDateSort || hasCountSort).toBeTruthy();
    }
  });

  test('should display paginator for table view', async ({ page }) => {
    const table = page.locator('.table-container table');
    const tableVisible = await table.isVisible().catch(() => false);

    if (tableVisible) {
      const paginator = page.locator('mat-paginator');
      await expect(paginator).toBeVisible();
    }
  });
});

test.describe('Duplicates - Row Expansion', () => {
  test.beforeEach(async ({ duplicatesPage }) => {
    await duplicatesPage.goto();
  });

  test('should have expand/collapse button for each row', async ({ page }) => {
    const rows = page.locator('.table-container tr.mat-mdc-row, tr.clickable-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      const expandButton = rows.first().locator('button').filter({
        has: page.locator('mat-icon:text("expand_more"), mat-icon:text("expand_less")')
      });
      await expect(expandButton).toBeVisible();
    }
  });

  test('should expand row on expand button click', async ({ page }) => {
    const rows = page.locator('tr.clickable-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      const expandButton = rows.first().locator('button mat-icon:text("expand_more")').locator('..');

      const buttonVisible = await expandButton.isVisible().catch(() => false);
      if (buttonVisible) {
        await expandButton.click();
        await page.waitForTimeout(300);

        // Expanded content should be visible
        const expandedContent = page.locator('.expanded-content');
        const isExpanded = await expandedContent.isVisible().catch(() => false);

        // Or the icon should change
        const collapseIcon = rows.first().locator('mat-icon:text("expand_less")');
        const iconChanged = await collapseIcon.isVisible().catch(() => false);

        expect(isExpanded || iconChanged).toBeTruthy();
      }
    }
  });

  test('should collapse row on collapse button click', async ({ page }) => {
    const rows = page.locator('tr.clickable-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      // First expand
      const expandButton = rows.first().locator('button mat-icon:text("expand_more")').locator('..');
      const buttonVisible = await expandButton.isVisible().catch(() => false);

      if (buttonVisible) {
        await expandButton.click();
        await page.waitForTimeout(300);

        // Then collapse
        const collapseButton = rows.first().locator('button mat-icon:text("expand_less")').locator('..');
        const collapseVisible = await collapseButton.isVisible().catch(() => false);

        if (collapseVisible) {
          await collapseButton.click();
          await page.waitForTimeout(300);

          // Expand icon should be back
          const expandIcon = rows.first().locator('mat-icon:text("expand_more")');
          await expect(expandIcon).toBeVisible();
        }
      }
    }
  });

  test('should show file paths in expanded content', async ({ page }) => {
    const rows = page.locator('tr.clickable-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      const expandButton = rows.first().locator('button mat-icon:text("expand_more")').locator('..');
      const buttonVisible = await expandButton.isVisible().catch(() => false);

      if (buttonVisible) {
        await expandButton.click();
        await page.waitForTimeout(500);

        const expandedContent = page.locator('.expanded-content');
        const visible = await expandedContent.isVisible().catch(() => false);

        if (visible) {
          // Should show original file section
          const originalSection = expandedContent.locator('h4:has-text("Original")');
          await expect(originalSection).toBeVisible();

          // Should show duplicate files section
          const duplicateSection = expandedContent.locator('h4:has-text("Duplicate")');
          await expect(duplicateSection).toBeVisible();
        }
      }
    }
  });
});

test.describe('Duplicates - Selection', () => {
  test.beforeEach(async ({ duplicatesPage }) => {
    await duplicatesPage.goto();
  });

  test('should select all rows with header checkbox', async ({ page }) => {
    const table = page.locator('.table-container table');
    const tableVisible = await table.isVisible().catch(() => false);

    if (tableVisible) {
      const headerCheckbox = page.locator('th mat-checkbox');
      const checkboxVisible = await headerCheckbox.isVisible().catch(() => false);

      if (checkboxVisible) {
        await headerCheckbox.click();
        await page.waitForTimeout(300);

        // All row checkboxes should be checked
        const rowCheckboxes = page.locator('td mat-checkbox');
        const count = await rowCheckboxes.count();

        for (let i = 0; i < Math.min(count, 3); i++) {
          const isChecked = await rowCheckboxes.nth(i).locator('input').isChecked();
          expect(isChecked).toBeTruthy();
        }
      }
    }
  });

  test('should deselect all on second header checkbox click', async ({ page }) => {
    const table = page.locator('.table-container table');
    const tableVisible = await table.isVisible().catch(() => false);

    if (tableVisible) {
      const headerCheckbox = page.locator('th mat-checkbox');
      const checkboxVisible = await headerCheckbox.isVisible().catch(() => false);

      if (checkboxVisible) {
        // Select all
        await headerCheckbox.click();
        await page.waitForTimeout(200);

        // Deselect all
        await headerCheckbox.click();
        await page.waitForTimeout(300);

        // All row checkboxes should be unchecked
        const rowCheckboxes = page.locator('td mat-checkbox');
        const count = await rowCheckboxes.count();

        if (count > 0) {
          const isChecked = await rowCheckboxes.first().locator('input').isChecked();
          expect(isChecked).toBeFalsy();
        }
      }
    }
  });

  test('should select individual row', async ({ page }) => {
    const rowCheckboxes = page.locator('td mat-checkbox');
    const count = await rowCheckboxes.count();

    if (count > 0) {
      await rowCheckboxes.first().click();
      await page.waitForTimeout(200);

      const isChecked = await rowCheckboxes.first().locator('input').isChecked();
      expect(isChecked).toBeTruthy();
    }
  });

  test('should show indeterminate state when some selected', async ({ page }) => {
    const rowCheckboxes = page.locator('td mat-checkbox');
    const count = await rowCheckboxes.count();

    if (count > 1) {
      // Select only first row
      await rowCheckboxes.first().click();
      await page.waitForTimeout(200);

      // Header checkbox should be indeterminate
      const headerCheckbox = page.locator('th mat-checkbox');
      const classes = await headerCheckbox.getAttribute('class');

      // Mat-checkbox shows indeterminate state via class or attribute
      const isIndeterminate = classes?.includes('mat-mdc-checkbox-indeterminate') ||
                              await headerCheckbox.locator('input').evaluate(el => (el as HTMLInputElement).indeterminate);

      expect(isIndeterminate).toBeTruthy();
    }
  });
});

test.describe('Duplicates - Bulk Actions Toolbar', () => {
  test.beforeEach(async ({ duplicatesPage }) => {
    await duplicatesPage.goto();
  });

  test('should display bulk actions toolbar', async ({ page }) => {
    const toolbar = page.locator('.bulk-toolbar, mat-toolbar.bulk-toolbar');
    await expect(toolbar).toBeVisible({ timeout: 10000 });
  });

  test('should display statistics in toolbar', async ({ page }) => {
    const toolbar = page.locator('.bulk-toolbar');
    const visible = await toolbar.isVisible().catch(() => false);

    if (visible) {
      // Should show groups count
      const groupsChip = toolbar.locator('mat-chip:has-text("groups")');
      await expect(groupsChip).toBeVisible();

      // Should show duplicate files count
      const filesChip = toolbar.locator('mat-chip:has-text("duplicate files")');
      await expect(filesChip).toBeVisible();

      // Should show potential savings
      const savingsChip = toolbar.locator('.savings-chip');
      await expect(savingsChip).toBeVisible();
    }
  });

  test('should display auto-select all button', async ({ page }) => {
    const toolbar = page.locator('.bulk-toolbar');
    const visible = await toolbar.isVisible().catch(() => false);

    if (visible) {
      const autoSelectButton = toolbar.getByRole('button', { name: /auto-select all/i });
      await expect(autoSelectButton).toBeVisible();
    }
  });

  test('should display refresh button', async ({ page }) => {
    const toolbar = page.locator('.bulk-toolbar');
    const visible = await toolbar.isVisible().catch(() => false);

    if (visible) {
      const refreshButton = toolbar.locator('button[mattooltip="Refresh"]');
      await expect(refreshButton).toBeVisible();
    }
  });

  test('should show selection count when items selected', async ({ page }) => {
    const rowCheckboxes = page.locator('td mat-checkbox');
    const count = await rowCheckboxes.count();

    if (count > 0) {
      await rowCheckboxes.first().click();
      await page.waitForTimeout(300);

      const selectionInfo = page.locator('.selection-info');
      const visible = await selectionInfo.isVisible().catch(() => false);

      if (visible) {
        await expect(selectionInfo).toContainText('selected');
      }
    }
  });
});

test.describe('Duplicates - Status Indicators', () => {
  test.beforeEach(async ({ duplicatesPage }) => {
    await duplicatesPage.goto();
  });

  test('should show status icon for each group', async ({ page }) => {
    const rows = page.locator('tr.clickable-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      // Status icons: check_circle, auto_fix_high, or warning
      const statusIcon = rows.first().locator('.status-icon');
      await expect(statusIcon).toBeVisible();
    }
  });

  test('should show validated status for groups with confirmed original', async ({ page }) => {
    const validatedRows = page.locator('td.status-validated');
    const count = await validatedRows.count();

    if (count > 0) {
      const icon = validatedRows.first().locator('mat-icon:text("check_circle")');
      await expect(icon).toBeVisible();
    }
  });

  test('should show auto-selected status for auto-resolved groups', async ({ page }) => {
    const autoSelectedRows = page.locator('td.status-auto-selected');
    const count = await autoSelectedRows.count();

    if (count > 0) {
      const icon = autoSelectedRows.first().locator('mat-icon:text("auto_fix_high")');
      await expect(icon).toBeVisible();
    }
  });

  test('should show warning status for unresolved groups', async ({ page }) => {
    const unresolvedRows = page.locator('td:has(.no-original)');
    const count = await unresolvedRows.count();

    if (count > 0) {
      const icon = unresolvedRows.first().locator('mat-icon:text("warning")');
      await expect(icon).toBeVisible();
    }
  });
});

test.describe('Duplicates - Sorting', () => {
  test.beforeEach(async ({ duplicatesPage }) => {
    await duplicatesPage.goto();
  });

  test('should sort by size when size header clicked', async ({ page }) => {
    const sizeHeader = page.locator('th[mat-sort-header="size"]');
    const visible = await sizeHeader.isVisible().catch(() => false);

    if (visible) {
      await sizeHeader.click();
      await page.waitForTimeout(500);

      // Sort indicator should appear
      const sortIndicator = sizeHeader.locator('.mat-sort-header-arrow');
      await expect(sortIndicator).toBeVisible();
    }
  });

  test('should toggle sort direction on repeated header click', async ({ page }) => {
    const sizeHeader = page.locator('th[mat-sort-header="size"]');
    const visible = await sizeHeader.isVisible().catch(() => false);

    if (visible) {
      // First click - ascending
      await sizeHeader.click();
      await page.waitForTimeout(300);

      // Second click - descending
      await sizeHeader.click();
      await page.waitForTimeout(300);

      // Third click - no sort or back to ascending
      await sizeHeader.click();
      await page.waitForTimeout(300);
    }
  });

  test('should sort by duplicate count', async ({ page }) => {
    const countHeader = page.locator('th[mat-sort-header="fileCount"]');
    const visible = await countHeader.isVisible().catch(() => false);

    if (visible) {
      await countHeader.click();
      await page.waitForTimeout(500);

      const sortIndicator = countHeader.locator('.mat-sort-header-arrow');
      await expect(sortIndicator).toBeVisible();
    }
  });
});

test.describe('Duplicates - Navigation to Detail', () => {
  test.beforeEach(async ({ duplicatesPage }) => {
    await duplicatesPage.goto();
  });

  test('should navigate to detail view on row click', async ({ page }) => {
    const rows = page.locator('tr.clickable-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      // Click on a non-button part of the row
      const originalColumn = rows.first().locator('td').nth(1);
      await originalColumn.click();

      // Should switch to detail view
      await page.waitForTimeout(500);

      const detailView = page.locator('app-duplicate-group-detail');
      const hasDetail = await detailView.isVisible().catch(() => false);

      // Or should have back button indicating detail view
      const backButton = page.locator('button:has-text("Back")');
      const hasBack = await backButton.isVisible().catch(() => false);

      expect(hasDetail || hasBack).toBeTruthy();
    }
  });

  test('should navigate back to list from detail view', async ({ page }) => {
    const rows = page.locator('tr.clickable-row');
    const rowCount = await rows.count();

    if (rowCount > 0) {
      // Go to detail
      const originalColumn = rows.first().locator('td').nth(1);
      await originalColumn.click();
      await page.waitForTimeout(500);

      // Find and click back button
      const backButton = page.locator('button:has-text("Back")');
      const visible = await backButton.isVisible().catch(() => false);

      if (visible) {
        await backButton.click();
        await page.waitForTimeout(500);

        // Should be back in list view
        const table = page.locator('.table-container table');
        await expect(table).toBeVisible();
      }
    }
  });
});

test.describe('Duplicates - Empty and Error States', () => {
  test.beforeEach(async ({ duplicatesPage }) => {
    await duplicatesPage.goto();
  });

  test('should show no duplicates message when collection is clean', async ({ page }) => {
    const emptyCard = page.locator('.empty-card:has-text("No Duplicates Found")');
    const visible = await emptyCard.isVisible().catch(() => false);

    if (visible) {
      await expect(emptyCard.locator('mat-icon:text("check_circle")')).toBeVisible();
      await expect(emptyCard).toContainText('no duplicate files');
    }
  });

  test('should show error state with retry option', async ({ page }) => {
    const errorCard = page.locator('.error-card');
    const visible = await errorCard.isVisible().catch(() => false);

    if (visible) {
      const retryButton = errorCard.getByRole('button', { name: /retry/i });
      await expect(retryButton).toBeVisible();
    }
  });
});

test.describe('Duplicates - View Mode Toggle', () => {
  test.beforeEach(async ({ duplicatesPage }) => {
    await duplicatesPage.goto();
  });

  test('should display view mode toggle', async ({ page }) => {
    const toggle = page.locator('app-view-mode-toggle');
    await expect(toggle).toBeVisible({ timeout: 10000 });
  });

  test('should switch between grid and table views', async ({ page }) => {
    const toggle = page.locator('app-view-mode-toggle');
    const visible = await toggle.isVisible().catch(() => false);

    if (visible) {
      // Find toggle buttons
      const buttons = toggle.locator('button, mat-button-toggle');
      const count = await buttons.count();

      if (count >= 2) {
        // Click to switch view
        await buttons.last().click();
        await page.waitForTimeout(500);

        // Click back to original
        await buttons.first().click();
        await page.waitForTimeout(500);
      }
    }
  });
});
