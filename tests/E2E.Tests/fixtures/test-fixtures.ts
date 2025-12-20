import { test as base, expect } from '@playwright/test';
import { DashboardPage } from '../page-objects/dashboard.page';
import { SettingsPage } from '../page-objects/settings.page';
import { FilesPage } from '../page-objects/files.page';
import { DuplicatesPage } from '../page-objects/duplicates.page';

/**
 * Extended Playwright test with page object fixtures
 * Usage: import { test, expect } from '../fixtures/test-fixtures'
 */

type Pages = {
  dashboardPage: DashboardPage;
  settingsPage: SettingsPage;
  filesPage: FilesPage;
  duplicatesPage: DuplicatesPage;
};

export const test = base.extend<Pages>({
  dashboardPage: async ({ page }, use) => {
    const dashboardPage = new DashboardPage(page);
    await use(dashboardPage);
  },

  settingsPage: async ({ page }, use) => {
    const settingsPage = new SettingsPage(page);
    await use(settingsPage);
  },

  filesPage: async ({ page }, use) => {
    const filesPage = new FilesPage(page);
    await use(filesPage);
  },

  duplicatesPage: async ({ page }, use) => {
    const duplicatesPage = new DuplicatesPage(page);
    await use(duplicatesPage);
  },
});

export { expect };
