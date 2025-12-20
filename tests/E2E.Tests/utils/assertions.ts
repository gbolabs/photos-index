import { expect, Locator, Page } from '@playwright/test';

/**
 * Custom assertion utilities for E2E tests
 * Provides reusable assertion helpers
 */

/**
 * Assert that an element contains a number greater than zero
 */
export async function expectPositiveNumber(locator: Locator): Promise<void> {
  const text = await locator.textContent();
  const number = parseInt(text?.replace(/[^0-9]/g, '') || '0');
  expect(number).toBeGreaterThan(0);
}

/**
 * Assert that an element displays a valid file size
 */
export async function expectValidFileSize(locator: Locator): Promise<void> {
  const text = await locator.textContent();
  expect(text).toMatch(/\d+(\.\d+)?\s*(B|KB|MB|GB|TB)/i);
}

/**
 * Assert that an element displays a valid date
 */
export async function expectValidDate(locator: Locator): Promise<void> {
  const text = await locator.textContent();
  // Match various date formats
  const datePattern = /\d{1,4}[-\/]\d{1,2}[-\/]\d{1,4}|\d{1,2}\/\d{1,2}\/\d{2,4}/;
  expect(text).toMatch(datePattern);
}

/**
 * Assert that a list has items
 */
export async function expectListNotEmpty(listLocator: Locator): Promise<void> {
  const count = await listLocator.count();
  expect(count).toBeGreaterThan(0);
}

/**
 * Assert that a page is loaded and visible
 */
export async function expectPageLoaded(page: Page): Promise<void> {
  await page.waitForLoadState('networkidle');
  await expect(page.locator('body')).toBeVisible();
}

/**
 * Assert that an element is visible and enabled
 */
export async function expectInteractive(locator: Locator): Promise<void> {
  await expect(locator).toBeVisible();
  await expect(locator).toBeEnabled();
}

/**
 * Assert that a table/list has a specific row count
 */
export async function expectRowCount(
  rowsLocator: Locator,
  expectedCount: number
): Promise<void> {
  const actualCount = await rowsLocator.count();
  expect(actualCount).toBe(expectedCount);
}

/**
 * Assert that a text matches a percentage pattern
 */
export async function expectValidPercentage(locator: Locator): Promise<void> {
  const text = await locator.textContent();
  expect(text).toMatch(/\d+(\.\d+)?%/);
}

/**
 * Assert that navigation completed successfully
 */
export async function expectNavigatedTo(
  page: Page,
  urlPattern: string | RegExp
): Promise<void> {
  await page.waitForLoadState('networkidle');
  await expect(page).toHaveURL(urlPattern);
}

/**
 * Assert that an error message is NOT displayed
 */
export async function expectNoErrors(page: Page): Promise<void> {
  const errorLocators = [
    page.locator('.error'),
    page.locator('.error-message'),
    page.locator('mat-error'),
    page.locator('[role="alert"]').filter({ hasText: /error/i }),
  ];

  for (const locator of errorLocators) {
    const count = await locator.count();
    if (count > 0) {
      await expect(locator.first()).not.toBeVisible();
    }
  }
}

/**
 * Assert that a success message is displayed
 */
export async function expectSuccessMessage(
  page: Page,
  messagePattern?: string | RegExp
): Promise<void> {
  const successLocator = page.locator(
    '.success, .success-message, mat-snack-bar-container'
  );

  await expect(successLocator).toBeVisible({ timeout: 5000 });

  if (messagePattern) {
    await expect(successLocator).toHaveText(messagePattern);
  }
}

/**
 * Assert that an element contains numeric data
 */
export async function expectNumericContent(locator: Locator): Promise<void> {
  const text = await locator.textContent();
  expect(text).toMatch(/\d+/);
}

/**
 * Assert that all elements in a list are visible
 */
export async function expectAllVisible(locator: Locator): Promise<void> {
  const count = await locator.count();
  expect(count).toBeGreaterThan(0);

  for (let i = 0; i < count; i++) {
    await expect(locator.nth(i)).toBeVisible();
  }
}

/**
 * Assert that a modal/dialog is open
 */
export async function expectModalOpen(page: Page): Promise<void> {
  const modalLocators = [
    page.locator('mat-dialog-container'),
    page.locator('.modal'),
    page.locator('[role="dialog"]'),
  ];

  let found = false;
  for (const locator of modalLocators) {
    const count = await locator.count();
    if (count > 0 && (await locator.first().isVisible())) {
      found = true;
      break;
    }
  }

  expect(found).toBeTruthy();
}

/**
 * Assert that a modal/dialog is closed
 */
export async function expectModalClosed(page: Page): Promise<void> {
  const modalLocators = [
    page.locator('mat-dialog-container'),
    page.locator('.modal'),
    page.locator('[role="dialog"]'),
  ];

  for (const locator of modalLocators) {
    const count = await locator.count();
    if (count > 0) {
      await expect(locator.first()).not.toBeVisible();
    }
  }
}

/**
 * Assert that loading spinner is not visible
 */
export async function expectNotLoading(page: Page): Promise<void> {
  const spinnerLocators = [
    page.locator('mat-spinner'),
    page.locator('.spinner'),
    page.locator('.loading'),
    page.locator('[data-testid="loading"]'),
  ];

  for (const locator of spinnerLocators) {
    const count = await locator.count();
    if (count > 0) {
      await expect(locator.first()).not.toBeVisible();
    }
  }
}

/**
 * Assert that a file path is valid
 */
export async function expectValidFilePath(locator: Locator): Promise<void> {
  const text = await locator.textContent();
  expect(text).toMatch(/^\/[\w\-\/\.]+$/);
}

/**
 * Assert that an image is loaded
 */
export async function expectImageLoaded(imageLocator: Locator): Promise<void> {
  await expect(imageLocator).toBeVisible();

  const naturalWidth = await imageLocator.evaluate(
    (img: HTMLImageElement) => img.naturalWidth
  );

  expect(naturalWidth).toBeGreaterThan(0);
}

/**
 * Assert that URL contains a specific parameter
 */
export async function expectUrlParam(
  page: Page,
  paramName: string,
  paramValue?: string
): Promise<void> {
  const url = new URL(page.url());
  const actualValue = url.searchParams.get(paramName);

  expect(actualValue).not.toBeNull();

  if (paramValue !== undefined) {
    expect(actualValue).toBe(paramValue);
  }
}

/**
 * Assert that local storage contains a specific key
 */
export async function expectLocalStorageKey(
  page: Page,
  key: string
): Promise<void> {
  const value = await page.evaluate((k) => localStorage.getItem(k), key);
  expect(value).not.toBeNull();
}

/**
 * Assert that response status is successful
 */
export async function expectSuccessfulResponse(
  responsePromise: Promise<any>
): Promise<void> {
  const response = await responsePromise;
  expect(response.ok()).toBeTruthy();
  expect(response.status()).toBeGreaterThanOrEqual(200);
  expect(response.status()).toBeLessThan(300);
}
