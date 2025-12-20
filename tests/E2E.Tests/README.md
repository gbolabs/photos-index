# PhotosIndex E2E Tests

Playwright-based end-to-end testing suite for the PhotosIndex application.

## Setup

### Prerequisites

- Node.js 22 or later
- npm

### Installation

```bash
# Install dependencies
npm install

# Install Playwright browsers
npx playwright install
```

## Running Tests

### All Tests

```bash
# Run all tests across all browsers
npm test

# Run tests in headed mode (visible browser)
npm run test:headed

# Run tests with UI mode (interactive)
npm run test:ui

# Run tests in debug mode
npm run test:debug
```

### Specific Browsers

```bash
# Chrome only
npm run test:chromium

# Firefox only
npm run test:firefox

# Safari only
npm run test:webkit

# Mobile viewports only
npm run test:mobile
```

### View Report

```bash
# Show HTML report
npm run report
```

## Project Structure

```
tests/E2E.Tests/
├── fixtures/              # Test fixtures and setup
│   ├── test-fixtures.ts   # Page object fixtures
│   └── api-fixtures.ts    # API helper fixtures
├── page-objects/          # Page Object Model
│   ├── base.page.ts       # Base page with common methods
│   ├── dashboard.page.ts  # Dashboard page object
│   ├── settings.page.ts   # Settings page object
│   ├── files.page.ts      # Files page object
│   └── duplicates.page.ts # Duplicates page object
├── tests/                 # Test specifications
│   └── smoke.spec.ts      # Smoke tests
├── utils/                 # Utility functions
│   ├── test-data.ts       # Test data generators
│   └── assertions.ts      # Custom assertions
├── playwright.config.ts   # Playwright configuration
├── tsconfig.json          # TypeScript configuration
└── package.json           # Node.js dependencies
```

## Configuration

### Environment Variables

- `BASE_URL` - Web application URL (default: http://localhost:8080)
- `API_URL` - API server URL (default: http://localhost:5000)
- `CI` - Set to enable CI-specific settings

### Example

```bash
BASE_URL=http://localhost:4200 npm test
```

## Writing Tests

### Using Page Objects

```typescript
import { test, expect } from '../fixtures/test-fixtures';

test('example test', async ({ dashboardPage }) => {
  await dashboardPage.goto();
  await dashboardPage.expectStatsVisible();
});
```

### Using API Fixtures

```typescript
import { apiTest } from '../fixtures/api-fixtures';

apiTest('example with API', async ({ apiContext, seedTestData }) => {
  await seedTestData();

  // Your test code here
});
```

## Browser Configuration

The test suite runs on:
- **Desktop**: Chrome, Firefox, Safari
- **Mobile**: Pixel 5, iPhone 12

## CI Integration

Tests are configured to run in CI with:
- 2 retries on failure
- Single worker for stability
- JUnit XML reports
- HTML reports with screenshots/videos on failure

## Best Practices

1. Use `data-testid` attributes in Angular components for stable selectors
2. Use page objects for all page interactions
3. Use fixtures to share common setup
4. Keep tests independent and isolated
5. Use meaningful test descriptions
6. Clean up test data after tests

## Troubleshooting

### Tests timing out

Increase timeout in `playwright.config.ts`:

```typescript
use: {
  actionTimeout: 20000,
  navigationTimeout: 60000,
}
```

### Browser not found

Reinstall browsers:

```bash
npx playwright install --with-deps
```

### Screenshots not captured

Check that `screenshot: 'only-on-failure'` is set in config.

## Contributing

When adding new tests:

1. Create page objects for new pages
2. Add fixtures for common setup
3. Write descriptive test names
4. Include both positive and negative test cases
5. Test across all configured browsers
