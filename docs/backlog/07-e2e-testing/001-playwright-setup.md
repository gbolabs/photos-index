# 001: Playwright E2E Setup

**Priority**: P3 (Quality Assurance)
**Agent**: A6
**Branch**: `feature/e2e-playwright-setup`
**Estimated Complexity**: Medium

## Objective

Set up Playwright for end-to-end testing with test fixtures, page objects, and CI integration.

## Dependencies

- `05-web-ui/002-dashboard.md` (UI must exist to test)
- All backend API endpoints deployed

## Acceptance Criteria

- [ ] Playwright installed and configured
- [ ] Test fixtures for authenticated/unauthenticated sessions
- [ ] Page Object Model for all pages
- [ ] Screenshots on failure
- [ ] Video recording option
- [ ] CI integration with artifact upload
- [ ] Cross-browser testing (Chrome, Firefox, Safari)
- [ ] Mobile viewport testing

## Files to Create

```
tests/E2E.Tests/
├── playwright.config.ts
├── package.json
├── tsconfig.json
├── fixtures/
│   ├── test-fixtures.ts
│   └── api-fixtures.ts
├── page-objects/
│   ├── base.page.ts
│   ├── dashboard.page.ts
│   ├── settings.page.ts
│   ├── files.page.ts
│   └── duplicates.page.ts
├── tests/
│   ├── smoke.spec.ts
│   └── ...
├── utils/
│   ├── test-data.ts
│   └── assertions.ts
└── .gitignore
```

## Configuration

```typescript
// playwright.config.ts
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [
    ['html'],
    ['junit', { outputFile: 'results/junit.xml' }]
  ],
  use: {
    baseURL: process.env.BASE_URL || 'http://localhost:8080',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
    {
      name: 'mobile-chrome',
      use: { ...devices['Pixel 5'] },
    },
    {
      name: 'mobile-safari',
      use: { ...devices['iPhone 12'] },
    },
  ],
  webServer: process.env.CI ? undefined : {
    command: 'npm run start',
    url: 'http://localhost:8080',
    reuseExistingServer: !process.env.CI,
  },
});
```

## Test Fixtures

```typescript
// fixtures/test-fixtures.ts
import { test as base, expect } from '@playwright/test';
import { DashboardPage } from '../page-objects/dashboard.page';
import { SettingsPage } from '../page-objects/settings.page';
import { FilesPage } from '../page-objects/files.page';
import { DuplicatesPage } from '../page-objects/duplicates.page';

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

// fixtures/api-fixtures.ts
import { test as base, APIRequestContext } from '@playwright/test';

type ApiFixtures = {
  apiContext: APIRequestContext;
  seedTestData: () => Promise<void>;
  cleanupTestData: () => Promise<void>;
};

export const apiTest = base.extend<ApiFixtures>({
  apiContext: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({
      baseURL: process.env.API_URL || 'http://localhost:5000',
    });
    await use(context);
    await context.dispose();
  },
  seedTestData: async ({ apiContext }, use) => {
    const seed = async () => {
      await apiContext.post('/api/scan-directories', {
        data: { path: '/test/photos', isEnabled: true }
      });
    };
    await use(seed);
  },
  cleanupTestData: async ({ apiContext }, use) => {
    const cleanup = async () => {
      // Delete all test data
    };
    await use(cleanup);
  },
});
```

## Page Objects

```typescript
// page-objects/base.page.ts
import { Page, Locator } from '@playwright/test';

export abstract class BasePage {
  readonly page: Page;
  readonly navDashboard: Locator;
  readonly navSettings: Locator;
  readonly navFiles: Locator;
  readonly navDuplicates: Locator;
  readonly loadingSpinner: Locator;

  constructor(page: Page) {
    this.page = page;
    this.navDashboard = page.getByRole('link', { name: 'Dashboard' });
    this.navSettings = page.getByRole('link', { name: 'Settings' });
    this.navFiles = page.getByRole('link', { name: 'Files' });
    this.navDuplicates = page.getByRole('link', { name: 'Duplicates' });
    this.loadingSpinner = page.locator('mat-spinner');
  }

  async waitForPageLoad(): Promise<void> {
    await this.loadingSpinner.waitFor({ state: 'hidden', timeout: 10000 });
  }

  async navigateToDashboard(): Promise<void> {
    await this.navDashboard.click();
    await this.waitForPageLoad();
  }

  async navigateToSettings(): Promise<void> {
    await this.navSettings.click();
    await this.waitForPageLoad();
  }
}

// page-objects/dashboard.page.ts
import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './base.page';

export class DashboardPage extends BasePage {
  readonly url = '/';
  readonly totalFilesCard: Locator;
  readonly storageUsedCard: Locator;
  readonly duplicatesCard: Locator;
  readonly savingsCard: Locator;
  readonly refreshButton: Locator;
  readonly directoryCards: Locator;

  constructor(page: Page) {
    super(page);
    this.totalFilesCard = page.locator('[data-testid="total-files"]');
    this.storageUsedCard = page.locator('[data-testid="storage-used"]');
    this.duplicatesCard = page.locator('[data-testid="duplicates"]');
    this.savingsCard = page.locator('[data-testid="savings"]');
    this.refreshButton = page.getByRole('button', { name: 'Refresh' });
    this.directoryCards = page.locator('.directory-card');
  }

  async goto(): Promise<void> {
    await this.page.goto(this.url);
    await this.waitForPageLoad();
  }

  async getTotalFiles(): Promise<string> {
    return await this.totalFilesCard.textContent() ?? '';
  }

  async getDuplicateCount(): Promise<string> {
    return await this.duplicatesCard.textContent() ?? '';
  }

  async refresh(): Promise<void> {
    await this.refreshButton.click();
    await this.waitForPageLoad();
  }

  async expectStatsVisible(): Promise<void> {
    await expect(this.totalFilesCard).toBeVisible();
    await expect(this.storageUsedCard).toBeVisible();
    await expect(this.duplicatesCard).toBeVisible();
    await expect(this.savingsCard).toBeVisible();
  }
}

// page-objects/settings.page.ts
import { Page, Locator } from '@playwright/test';
import { BasePage } from './base.page';

export class SettingsPage extends BasePage {
  readonly url = '/settings';
  readonly addDirectoryButton: Locator;
  readonly directoryList: Locator;
  readonly pathInput: Locator;
  readonly saveButton: Locator;
  readonly cancelButton: Locator;

  constructor(page: Page) {
    super(page);
    this.addDirectoryButton = page.getByRole('button', { name: 'Add Directory' });
    this.directoryList = page.locator('.directory-list');
    this.pathInput = page.locator('input[formcontrolname="path"]');
    this.saveButton = page.getByRole('button', { name: /Add|Save/ });
    this.cancelButton = page.getByRole('button', { name: 'Cancel' });
  }

  async goto(): Promise<void> {
    await this.page.goto(this.url);
    await this.waitForPageLoad();
  }

  async addDirectory(path: string): Promise<void> {
    await this.addDirectoryButton.click();
    await this.pathInput.fill(path);
    await this.saveButton.click();
    await this.waitForPageLoad();
  }

  async deleteDirectory(path: string): Promise<void> {
    const row = this.page.locator('.directory-row', { hasText: path });
    await row.getByRole('button', { name: 'Delete' }).click();
    await this.page.getByRole('button', { name: 'Confirm' }).click();
    await this.waitForPageLoad();
  }
}
```

## Smoke Test

```typescript
// tests/smoke.spec.ts
import { test, expect } from '../fixtures/test-fixtures';

test.describe('Smoke Tests', () => {
  test('dashboard loads successfully', async ({ dashboardPage }) => {
    await dashboardPage.goto();
    await dashboardPage.expectStatsVisible();
  });

  test('navigation works', async ({ dashboardPage, page }) => {
    await dashboardPage.goto();

    await dashboardPage.navigateToSettings();
    await expect(page).toHaveURL('/settings');

    await dashboardPage.navigateToDashboard();
    await expect(page).toHaveURL('/');
  });

  test('app is responsive on mobile', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });
    await page.goto('/');
    await expect(page.locator('.dashboard-container')).toBeVisible();
  });
});
```

## Package.json

```json
{
  "name": "e2e-tests",
  "scripts": {
    "test": "playwright test",
    "test:headed": "playwright test --headed",
    "test:debug": "playwright test --debug",
    "test:ui": "playwright test --ui",
    "report": "playwright show-report"
  },
  "devDependencies": {
    "@playwright/test": "^1.48.0",
    "typescript": "~5.6.0"
  }
}
```

## CI Integration

```yaml
# Add to .github/workflows/pr.yml
e2e-tests:
  runs-on: ubuntu-latest
  needs: [backend-build, frontend-build]
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-node@v4
      with:
        node-version: '22'
    - name: Install Playwright
      run: |
        cd tests/E2E.Tests
        npm ci
        npx playwright install --with-deps
    - name: Run E2E tests
      run: |
        cd tests/E2E.Tests
        npm test
      env:
        BASE_URL: http://localhost:8080
        API_URL: http://localhost:5000
    - uses: actions/upload-artifact@v4
      if: always()
      with:
        name: playwright-report
        path: tests/E2E.Tests/playwright-report/
```

## Test Coverage

- Page objects: All major pages covered
- Smoke tests: 100% of critical paths
- Cross-browser: Chrome, Firefox, Safari

## Completion Checklist

- [ ] Initialize Playwright project
- [ ] Create playwright.config.ts
- [ ] Create test fixtures
- [ ] Create API fixtures for data seeding
- [ ] Create BasePage with common navigation
- [ ] Create DashboardPage page object
- [ ] Create SettingsPage page object
- [ ] Create FilesPage page object
- [ ] Create DuplicatesPage page object
- [ ] Write smoke tests
- [ ] Add data-testid attributes to Angular components
- [ ] Configure cross-browser testing
- [ ] Configure mobile viewport testing
- [ ] Add CI workflow step
- [ ] All tests passing locally
- [ ] PR created and reviewed
