# 003: Playwright CI Integration

**Status**: ✅ Complete
**PR**: [#87](https://github.com/gbolabs/photos-index/pull/87)
**Priority**: P2 (Important)
**Agent**: A6
**Branch**: `feature/playwright-ci`
**Estimated Complexity**: Low

## Objective

Add Playwright E2E tests to the CI/CD pipeline so tests run automatically on PRs.

## Dependencies

- `07-e2e-testing/001-playwright-setup.md` (completed)
- Sandbox image with Playwright (this PR)

## Acceptance Criteria

- [x] E2E tests job added to `.github/workflows/pr.yml`
- [x] Job runs after backend and frontend builds
- [x] Docker Compose starts full stack for testing
- [x] Test reports uploaded as artifacts
- [x] Screenshots/videos captured on failure

## Implementation

Add to `.github/workflows/pr.yml`:

```yaml
e2e-tests:
  name: E2E Tests (Playwright)
  runs-on: ubuntu-latest
  needs: [build-backend, build-frontend]

  steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Setup Node.js
      uses: actions/setup-node@v4
      with:
        node-version: ${{ env.NODE_VERSION }}
        cache: 'npm'
        cache-dependency-path: tests/E2E.Tests/package-lock.json

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Install Playwright dependencies
      working-directory: tests/E2E.Tests
      run: |
        npm ci
        npx playwright install --with-deps chromium

    - name: Start services with Docker Compose
      run: |
        docker compose -f deploy/docker/docker-compose.yml up -d --build
        echo "Waiting for services to be ready..."
        timeout 120 bash -c 'until curl -sf http://localhost:5000/health; do sleep 2; done' || true
        timeout 60 bash -c 'until curl -sf http://localhost:4200; do sleep 2; done' || true

    - name: Run Playwright tests
      working-directory: tests/E2E.Tests
      run: npx playwright test --project=chromium
      env:
        BASE_URL: http://localhost:80
        API_URL: http://localhost:5000
        CI: true

    - name: Stop services
      if: always()
      run: docker compose -f deploy/docker/docker-compose.yml down -v

    - name: Upload Playwright report
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: playwright-report
        path: tests/E2E.Tests/playwright-report/
        retention-days: 7

    - name: Upload test results
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: playwright-results
        path: |
          tests/E2E.Tests/test-results/
          tests/E2E.Tests/results/
        retention-days: 7
```

## Blocked By

Requires GitHub token with `workflow` scope to push changes to `.github/workflows/`.
Once `feature/playwright-sandbox` PR is merged and sandbox is rebuilt, this can be pushed.

## Completion Checklist

- [x] Add e2e-tests job to pr.yml
- [ ] Test locally with act or manual workflow trigger
- [x] Verify artifacts are uploaded
- [x] PR created and merged
