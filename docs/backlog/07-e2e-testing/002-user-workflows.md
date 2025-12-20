# 002: User Workflow E2E Tests

**Priority**: P3 (Quality Assurance)
**Agent**: A6
**Branch**: `feature/e2e-user-workflows`
**Estimated Complexity**: Medium

## Objective

Implement comprehensive E2E tests covering critical user workflows.

## Dependencies

- `07-e2e-testing/001-playwright-setup.md`
- All UI components implemented

## Acceptance Criteria

- [ ] Directory management workflow tests
- [ ] File browsing and filtering tests
- [ ] Duplicate management workflow tests
- [ ] Error handling and edge case tests
- [ ] Performance tests (page load times)
- [ ] Accessibility tests (keyboard navigation, screen readers)

## Test Scenarios

### Directory Management

```typescript
// tests/directories.spec.ts
import { test, expect } from '../fixtures/test-fixtures';
import { apiTest } from '../fixtures/api-fixtures';

test.describe('Directory Management', () => {
  test.beforeEach(async ({ settingsPage }) => {
    await settingsPage.goto();
  });

  test('add new directory', async ({ settingsPage, page }) => {
    await settingsPage.addDirectory('/photos/family');

    await expect(page.locator('.directory-row', { hasText: '/photos/family' }))
      .toBeVisible();
  });

  test('edit existing directory', async ({ settingsPage, page }) => {
    // Seed a directory first
    await settingsPage.addDirectory('/photos/edit-me');

    // Edit it
    const row = page.locator('.directory-row', { hasText: '/photos/edit-me' });
    await row.getByRole('button', { name: 'Edit' }).click();

    await page.locator('input[formcontrolname="path"]').fill('/photos/edited');
    await page.getByRole('button', { name: 'Save' }).click();

    await expect(page.locator('.directory-row', { hasText: '/photos/edited' }))
      .toBeVisible();
  });

  test('delete directory with confirmation', async ({ settingsPage, page }) => {
    await settingsPage.addDirectory('/photos/delete-me');
    await settingsPage.deleteDirectory('/photos/delete-me');

    await expect(page.locator('.directory-row', { hasText: '/photos/delete-me' }))
      .not.toBeVisible();
  });

  test('prevent duplicate paths', async ({ settingsPage, page }) => {
    await settingsPage.addDirectory('/photos/unique');
    await settingsPage.addDirectoryButton.click();
    await settingsPage.pathInput.fill('/photos/unique');
    await settingsPage.saveButton.click();

    await expect(page.getByText(/already exists|duplicate/i)).toBeVisible();
  });

  test('validate path format', async ({ settingsPage, page }) => {
    await settingsPage.addDirectoryButton.click();
    await settingsPage.pathInput.fill('relative/path');

    await expect(page.getByText(/must be an absolute path/i)).toBeVisible();
    await expect(settingsPage.saveButton).toBeDisabled();
  });

  test('toggle directory enabled state', async ({ settingsPage, page }) => {
    await settingsPage.addDirectory('/photos/toggle');

    const row = page.locator('.directory-row', { hasText: '/photos/toggle' });
    const toggle = row.locator('mat-slide-toggle');

    await toggle.click();
    await expect(toggle).toHaveAttribute('aria-checked', 'false');

    await toggle.click();
    await expect(toggle).toHaveAttribute('aria-checked', 'true');
  });

  test('trigger manual scan', async ({ settingsPage, page }) => {
    await settingsPage.addDirectory('/photos/scan');

    const row = page.locator('.directory-row', { hasText: '/photos/scan' });
    await row.getByRole('button', { name: 'Scan' }).click();

    await expect(page.getByText(/scan started/i)).toBeVisible();
  });
});
```

### File Browsing

```typescript
// tests/files.spec.ts
import { test, expect } from '../fixtures/test-fixtures';

test.describe('File Browsing', () => {
  test.beforeEach(async ({ filesPage, apiContext }) => {
    // Seed test files via API
    await apiContext.post('/api/files/batch', {
      data: {
        files: Array.from({ length: 25 }, (_, i) => ({
          filePath: `/photos/test${i}.jpg`,
          fileName: `test${i}.jpg`,
          sha256Hash: `hash${i}`,
          fileSizeBytes: 1024 * (i + 1)
        })),
        scanDirectoryId: 'test-dir-id'
      }
    });
    await filesPage.goto();
  });

  test('display files in grid view', async ({ filesPage, page }) => {
    await expect(page.locator('.file-card')).toHaveCount(20); // Default page size
  });

  test('switch between grid and list view', async ({ filesPage, page }) => {
    await page.getByRole('button', { name: 'List view' }).click();
    await expect(page.locator('.file-table-row')).toBeVisible();

    await page.getByRole('button', { name: 'Grid view' }).click();
    await expect(page.locator('.file-card')).toBeVisible();
  });

  test('paginate through files', async ({ filesPage, page }) => {
    await page.getByRole('button', { name: 'Next page' }).click();
    await expect(page.locator('.file-card')).toHaveCount(5); // 25 total, 20 per page
  });

  test('search files by name', async ({ filesPage, page }) => {
    await page.getByPlaceholder('Search...').fill('test10');
    await page.waitForTimeout(500); // Debounce

    await expect(page.locator('.file-card')).toHaveCount(1);
    await expect(page.locator('.file-card')).toContainText('test10.jpg');
  });

  test('filter by directory', async ({ filesPage, page }) => {
    await page.getByLabel('Directory').click();
    await page.getByRole('option', { name: '/photos' }).click();

    await filesPage.waitForPageLoad();
    await expect(page.locator('.file-card')).toBeVisible();
  });

  test('filter duplicates only', async ({ filesPage, page }) => {
    await page.getByLabel('Duplicates only').check();
    await filesPage.waitForPageLoad();
    // Should show only files with duplicateGroupId
  });

  test('sort by different fields', async ({ filesPage, page }) => {
    await page.getByLabel('Sort by').click();
    await page.getByRole('option', { name: 'File size' }).click();

    await filesPage.waitForPageLoad();
    // Verify order changed
  });

  test('show file details on click', async ({ filesPage, page }) => {
    await page.locator('.file-card').first().click();

    await expect(page.locator('.file-details-panel')).toBeVisible();
    await expect(page.getByText('File path')).toBeVisible();
    await expect(page.getByText('File size')).toBeVisible();
  });

  test('lazy load thumbnails', async ({ filesPage, page }) => {
    // Scroll and verify thumbnails load
    const cards = page.locator('.file-card');
    const lastCard = cards.last();

    await lastCard.scrollIntoViewIfNeeded();
    await expect(lastCard.locator('img')).toBeVisible();
  });
});
```

### Duplicate Management

```typescript
// tests/duplicates.spec.ts
import { test, expect } from '../fixtures/test-fixtures';

test.describe('Duplicate Management', () => {
  test.beforeEach(async ({ duplicatesPage, apiContext }) => {
    // Seed duplicate groups via API
    await apiContext.post('/api/test/seed-duplicates', {
      data: { groupCount: 5, filesPerGroup: 3 }
    });
    await duplicatesPage.goto();
  });

  test('display duplicate groups', async ({ duplicatesPage, page }) => {
    await expect(page.locator('.duplicate-group')).toHaveCount(5);
  });

  test('show group details on select', async ({ duplicatesPage, page }) => {
    await page.locator('.duplicate-group').first().click();

    await expect(page.locator('.group-detail')).toBeVisible();
    await expect(page.locator('.file-item')).toHaveCount(3);
  });

  test('highlight original file', async ({ duplicatesPage, page }) => {
    await page.locator('.duplicate-group').first().click();

    await expect(page.locator('.file-item.is-original')).toHaveCount(1);
    await expect(page.locator('.original-badge')).toBeVisible();
  });

  test('set different file as original', async ({ duplicatesPage, page }) => {
    await page.locator('.duplicate-group').first().click();

    const nonOriginal = page.locator('.file-item:not(.is-original)').first();
    await nonOriginal.getByRole('button', { name: 'Set as Original' }).click();

    await expect(nonOriginal).toHaveClass(/is-original/);
  });

  test('auto-select original', async ({ duplicatesPage, page }) => {
    await page.locator('.duplicate-group').first().click();
    await page.getByRole('button', { name: 'Auto-select Original' }).click();

    await expect(page.getByText(/original.*selected/i)).toBeVisible();
  });

  test('delete non-originals with confirmation', async ({ duplicatesPage, page }) => {
    await page.locator('.duplicate-group').first().click();
    await page.getByRole('button', { name: 'Delete Duplicates' }).click();

    // Confirmation dialog
    await expect(page.getByText(/move.*to trash/i)).toBeVisible();
    await page.getByRole('button', { name: 'Delete' }).click();

    await expect(page.getByText(/files queued for deletion/i)).toBeVisible();
  });

  test('compare images side by side', async ({ duplicatesPage, page }) => {
    await page.locator('.duplicate-group').first().click();

    const images = page.locator('.file-item img');
    await expect(images).toHaveCount(3);
  });

  test('preview image at full size', async ({ duplicatesPage, page }) => {
    await page.locator('.duplicate-group').first().click();
    await page.locator('.file-item').first().getByRole('button', { name: 'Preview' }).click();

    await expect(page.locator('.image-preview-dialog')).toBeVisible();
  });

  test('bulk auto-select all groups', async ({ duplicatesPage, page }) => {
    await page.getByRole('button', { name: 'Bulk mode' }).click();

    // Select all groups
    await page.getByLabel('Select all').check();

    await page.getByRole('button', { name: 'Auto-select all' }).click();
    await expect(page.getByText(/auto-selected.*groups/i)).toBeVisible();
  });
});
```

### Error Handling

```typescript
// tests/errors.spec.ts
import { test, expect } from '../fixtures/test-fixtures';

test.describe('Error Handling', () => {
  test('show error when API is unreachable', async ({ page }) => {
    // Block API requests
    await page.route('**/api/**', route => route.abort());

    await page.goto('/');
    await expect(page.getByText(/cannot connect|failed to load/i)).toBeVisible();
  });

  test('show error for 404 responses', async ({ page }) => {
    await page.goto('/files/non-existent-id');
    await expect(page.getByText(/not found/i)).toBeVisible();
  });

  test('retry button works after error', async ({ page }) => {
    let blocked = true;

    await page.route('**/api/files/stats', route => {
      if (blocked) {
        route.abort();
      } else {
        route.continue();
      }
    });

    await page.goto('/');
    await expect(page.getByText(/failed to load/i)).toBeVisible();

    blocked = false;
    await page.getByRole('button', { name: 'Retry' }).click();

    await expect(page.getByText(/failed to load/i)).not.toBeVisible();
  });
});
```

### Accessibility

```typescript
// tests/accessibility.spec.ts
import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test.describe('Accessibility', () => {
  test('dashboard has no accessibility violations', async ({ page }) => {
    await page.goto('/');

    const results = await new AxeBuilder({ page }).analyze();
    expect(results.violations).toEqual([]);
  });

  test('keyboard navigation works', async ({ page }) => {
    await page.goto('/');

    // Tab through navigation
    await page.keyboard.press('Tab');
    await expect(page.locator(':focus')).toHaveAttribute('role', 'link');

    // Navigate with Enter
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(/settings|files|duplicates/);
  });

  test('focus is trapped in dialogs', async ({ page }) => {
    await page.goto('/settings');
    await page.getByRole('button', { name: 'Add Directory' }).click();

    // Tab should stay within dialog
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');

    const focused = page.locator(':focus');
    await expect(focused).toBeVisible();
    // Focus should still be in dialog
  });
});
```

## Test Coverage

- Directory management: 90% of user actions
- File browsing: 85% of user actions
- Duplicate management: 90% of user actions
- Error handling: All error states
- Accessibility: WCAG 2.1 AA compliance

## Completion Checklist

- [ ] Write directory management tests
- [ ] Write file browsing tests
- [ ] Write duplicate management tests
- [ ] Write error handling tests
- [ ] Write accessibility tests with axe-core
- [ ] Write keyboard navigation tests
- [ ] Add test data seeding utilities
- [ ] Ensure tests are independent
- [ ] Add retry logic for flaky tests
- [ ] All tests passing
- [ ] PR created and reviewed
