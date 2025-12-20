import { test as base, APIRequestContext } from '@playwright/test';

/**
 * API test fixtures for data seeding and cleanup
 * Usage: import { apiTest } from '../fixtures/api-fixtures'
 */

type ApiFixtures = {
  apiContext: APIRequestContext;
  seedTestData: () => Promise<void>;
  cleanupTestData: () => Promise<void>;
};

export const apiTest = base.extend<ApiFixtures>({
  apiContext: async ({ playwright }, use) => {
    const context = await playwright.request.newContext({
      baseURL: process.env.API_URL || 'http://localhost:5000',
      extraHTTPHeaders: {
        'Content-Type': 'application/json',
      },
    });
    await use(context);
    await context.dispose();
  },

  seedTestData: async ({ apiContext }, use) => {
    const seed = async () => {
      // Example: Create test scan directories
      const response = await apiContext.post('/api/scan-directories', {
        data: {
          path: '/test/photos',
          isEnabled: true,
          description: 'Test directory for E2E tests'
        }
      });

      if (!response.ok()) {
        console.warn(`Failed to seed test data: ${response.status()} ${await response.text()}`);
      }

      // Add more test data seeding as needed
      // For example: create test files, duplicate groups, etc.
    };
    await use(seed);
  },

  cleanupTestData: async ({ apiContext }, use) => {
    const cleanup = async () => {
      // Example: Delete test data
      try {
        // Clean up scan directories created during tests
        const directories = await apiContext.get('/api/scan-directories');
        if (directories.ok()) {
          const data = await directories.json();
          const testDirs = data.filter((d: any) => d.path?.includes('/test/'));

          for (const dir of testDirs) {
            await apiContext.delete(`/api/scan-directories/${dir.id}`);
          }
        }
      } catch (error) {
        console.warn('Failed to cleanup test data:', error);
      }

      // Add more cleanup logic as needed
    };
    await use(cleanup);
  },
});
