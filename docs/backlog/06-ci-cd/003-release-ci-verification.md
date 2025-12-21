# Release Pipeline CI Verification

## Problem Statement

The release workflow triggers immediately on tag push without verifying that CI passed on the tagged commit. This could publish broken container images.

## Current Flow

```
1. PR merged to main
2. Manual: git tag v0.0.x && git push origin v0.0.x
3. Release workflow builds and pushes images
   (no check if CI passed)
```

## Proposed Solution

Add a "verify-ci" job that blocks image builds until CI status is confirmed:

```yaml
jobs:
  verify-ci:
    runs-on: ubuntu-latest
    steps:
      - name: Check CI status on commit
        run: |
          STATUS=$(gh api repos/${{ github.repository }}/commits/${{ github.sha }}/status --jq '.state')
          if [ "$STATUS" != "success" ]; then
            echo "CI has not passed on this commit (status: $STATUS)"
            exit 1
          fi
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}

  build-api:
    needs: verify-ci
    # ... rest of build

  build-web:
    needs: verify-ci
    # ... rest of build
```

## Alternative Approaches

1. **workflow_run trigger**: Wait for CI workflow to complete before release
2. **Branch protection**: Require status checks (doesn't apply to tags on free plan)
3. **Composite action**: Reusable action for CI verification

## Files to Modify

- `.github/workflows/release.yml`

## Priority

**Medium** - Prevents publishing broken images

## Effort

1 hour
