# Container Image Retention Policy

## Problem Statement

GitHub Container Registry (ghcr.io) has no built-in retention policy. Over time, the registry could accumulate many unused image versions, especially if we introduce:
- Development/feature branch builds
- Nightly builds
- PR preview builds

## Current State

- Only tagged releases (v0.0.1, v0.0.2, v0.0.3) are pushed
- Public repo = unlimited storage (no cost concern)
- All versions kept indefinitely

## Proposed Solution

Add a scheduled GitHub Action to prune old container images while keeping:
- All semantically versioned tags (v*.*.*)
- The N most recent untagged versions
- Images younger than X days

## Implementation

### Workflow File

```yaml
# .github/workflows/cleanup-images.yml
name: Cleanup Container Images

on:
  schedule:
    - cron: '0 0 * * 0'  # Weekly on Sunday
  workflow_dispatch:  # Manual trigger

jobs:
  cleanup:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        image:
          - api
          - web
          - indexing-service
          - cleaner-service
    steps:
      - name: Delete old container images
        uses: snok/container-retention-policy@v3
        with:
          account: gbolabs
          token: ${{ secrets.GITHUB_TOKEN }}
          image-names: "photos-index/${{ matrix.image }}"
          tag-selection: untagged
          cut-off: 2 weeks ago UTC
          keep-n-most-recent: 5
```

### Configuration Options

| Setting | Value | Rationale |
|---------|-------|-----------|
| Schedule | Weekly (Sunday) | Low frequency, cleanup during low activity |
| Keep N recent | 5 | Allow rollback to recent versions |
| Cut-off | 2 weeks | Balance between storage and rollback needs |
| Tag selection | untagged | Preserve all semantic version tags |

## When to Implement

**Trigger**: When any of these conditions are met:
- We start building on every PR
- We add nightly/dev builds
- Storage usage becomes a concern
- More than 20 untagged versions accumulate

## Rollback Considerations

- Keep all tagged releases (v*.*.*) forever
- Ensure Kubernetes deployments can roll back
- Consider node image caching behavior

## Alternative Approaches

1. **Manual cleanup**: Use GitHub UI to delete old versions
2. **actions/delete-package-versions**: Alternative action with different API
3. **No cleanup**: Accept unlimited growth (viable for public repos)

## Dependencies

- `snok/container-retention-policy@v3` GitHub Action
- `GITHUB_TOKEN` with `packages:delete` permission

## Priority

**Low** - Not needed until we have more build activity

## Effort

1-2 hours
