# 004: GitHub Release Cleanup

## Problem

The container image cleanup workflow (`cleanup-images.yml`) only removes old untagged container images from ghcr.io. It does not clean up old GitHub releases.

Over time, the releases page will accumulate many versions (v0.0.1, v0.0.2, etc.) that may no longer be relevant.

## Proposed Solution

Add a GitHub release cleanup job to the existing cleanup workflow, or create a separate workflow.

### Options

**Option A: Keep all releases (current)**
- Releases are lightweight (just git tags + metadata)
- Provides full version history
- No cleanup needed

**Option B: Delete pre-release/draft releases older than N days**
- Keep all tagged releases (v*.*.*)
- Delete draft releases older than 30 days
- Delete pre-releases older than 90 days

**Option C: Keep only last N releases per major version**
- Keep last 5 releases per major version
- e.g., keep v0.0.5, v0.0.4, v0.0.3, v0.0.2, v0.0.1 but delete v0.0.0

### Implementation (Option B)

```yaml
# Add to cleanup-images.yml or new workflow
cleanup-releases:
  name: Cleanup old releases
  runs-on: ubuntu-latest
  steps:
    - name: Delete old draft releases
      uses: dev-drprasad/delete-older-releases@v0.3.2
      with:
        keep_latest: 10
        delete_tags: false
        delete_tag_pattern: ""
        delete_prerelease_only: true
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

## Recommendation

**Option A (keep all)** is fine for now. GitHub releases are cheap and provide useful history. Revisit if the releases page becomes unwieldy (50+ releases).

## Priority

Low - not urgent, GitHub releases don't consume significant resources.
