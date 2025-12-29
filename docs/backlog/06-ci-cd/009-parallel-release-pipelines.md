# Parallel Release Pipelines per Service

## Problem Statement

The current release workflow (`release.yml`) builds all 6 container images in a single workflow using a matrix strategy. While the matrix jobs run in parallel, this approach has limitations:

1. **Monolithic workflow**: All images share the same workflow file, making it harder to debug individual service builds
2. **Single point of failure**: If one image build fails, the entire release is blocked
3. **Slower iteration**: Cannot re-run a single failed image build; must re-run all jobs
4. **Resource contention**: All jobs compete for GitHub Actions runners simultaneously
5. **Coupling**: Adding a new service requires modifying the shared workflow

## Current Architecture

```
release.yml (on tag push)
├── matrix: [api, web, indexing-service, cleaner-service, metadata-service, thumbnail-service]
│   ├── Build & Push (parallel x6)
│   └── All use same cache namespace
└── create-release (needs: build-and-push)
    └── Creates GitHub Release with changelog
```

## Proposed Architecture

Split into independent workflows per service, coordinated by a release orchestrator:

```
on tag push v*
├── release-api.yml          → ghcr.io/.../api:version
├── release-web.yml          → ghcr.io/.../web:version
├── release-indexing.yml     → ghcr.io/.../indexing-service:version
├── release-cleaner.yml      → ghcr.io/.../cleaner-service:version
├── release-metadata.yml     → ghcr.io/.../metadata-service:version
├── release-thumbnail.yml    → ghcr.io/.../thumbnail-service:version
└── release-notes.yml        → Creates GitHub Release (triggered after all complete)
```

## Design Considerations

### 1. Workflow Triggering

Each service workflow should trigger on the same tag pattern:

```yaml
on:
  push:
    tags:
      - 'v*'
```

**Consideration**: All workflows will trigger simultaneously on tag push, which is the desired behavior for parallel execution.

### 2. Release Notes Coordination

The GitHub Release creation must wait for all image builds to complete:

**Option A: `workflow_run` trigger**
```yaml
# release-notes.yml
on:
  workflow_run:
    workflows: [Release API, Release Web, Release Indexing, ...]
    types: [completed]
    branches: [main]
```
- **Issue**: `workflow_run` triggers on each workflow completion, not when all complete
- **Workaround**: Check if all expected workflows have succeeded before creating release

**Option B: Repository Dispatch**
```yaml
# Each service workflow ends with:
- name: Signal completion
  run: |
    gh api repos/${{ github.repository }}/dispatches \
      -f event_type=image-built \
      -f client_payload='{"service":"api","version":"${{ steps.version.outputs.version }}"}'
```
- Release notes workflow collects signals and creates release when all are received
- **Issue**: Complex state management, race conditions

**Option C: Polling workflow**
```yaml
# release-notes.yml runs after a delay and checks all image manifests exist
on:
  push:
    tags:
      - 'v*'

jobs:
  wait-for-images:
    runs-on: ubuntu-latest
    steps:
      - name: Wait for all images
        run: |
          for service in api web indexing-service cleaner-service metadata-service thumbnail-service; do
            until docker manifest inspect ghcr.io/${{ github.repository }}/$service:${{ github.ref_name }} 2>/dev/null; do
              echo "Waiting for $service..."
              sleep 30
            done
          done
```
- Simple but adds latency
- Good fallback if other options are complex

**Recommended**: Option C with timeout - simple, reliable, no coordination complexity

### 3. Failure Handling

With independent pipelines:
- A single failed build doesn't block others
- Failed builds can be re-triggered independently via `gh workflow run`
- Release notes workflow should handle partial failures gracefully

```yaml
# release-notes.yml
- name: Check image availability
  id: check
  run: |
    MISSING=""
    for service in api web indexing-service cleaner-service metadata-service thumbnail-service; do
      if ! docker manifest inspect ghcr.io/.../service:$VERSION 2>/dev/null; then
        MISSING="$MISSING $service"
      fi
    done
    echo "missing=$MISSING" >> $GITHUB_OUTPUT

- name: Create release
  if: steps.check.outputs.missing == ''
  # Create full release

- name: Create partial release
  if: steps.check.outputs.missing != ''
  # Create release with warning about missing images
```

### 4. Caching Strategy

Each workflow maintains its own cache namespace:
```yaml
cache-from: type=gha,scope=release-api
cache-to: type=gha,mode=max,scope=release-api
```

Benefits:
- No cache conflicts between services
- Each service's cache can warm independently
- Easier cache invalidation per service

### 5. Path Filters (Future Enhancement)

For CI builds (not releases), consider path-based triggering:
```yaml
# ci-api.yml
on:
  push:
    paths:
      - 'src/Api/**'
      - 'src/Shared/**'
      - 'deploy/docker/api/**'
```

**Note**: Path filters should NOT apply to release workflows - all services should build on every release tag.

### 6. Reusable Workflows

Extract common build logic into a reusable workflow:
```yaml
# .github/workflows/build-image.yml
on:
  workflow_call:
    inputs:
      service-name:
        required: true
        type: string
      dockerfile:
        required: true
        type: string
```

Each service workflow becomes minimal:
```yaml
# release-api.yml
jobs:
  build:
    uses: ./.github/workflows/build-image.yml
    with:
      service-name: api
      dockerfile: deploy/docker/api/Dockerfile
```

## Migration Plan

1. Create reusable `build-image.yml` workflow
2. Create individual `release-{service}.yml` workflows
3. Create `release-notes.yml` with image availability check
4. Test with a pre-release tag (e.g., `v0.4.0-rc1`)
5. Remove matrix from `release.yml`
6. Rename/archive old `release.yml`

## Files to Create/Modify

- `.github/workflows/build-image.yml` (new - reusable)
- `.github/workflows/release-api.yml` (new)
- `.github/workflows/release-web.yml` (new)
- `.github/workflows/release-indexing.yml` (new)
- `.github/workflows/release-cleaner.yml` (new)
- `.github/workflows/release-metadata.yml` (new)
- `.github/workflows/release-thumbnail.yml` (new)
- `.github/workflows/release-notes.yml` (new)
- `.github/workflows/release.yml` (archive/remove)

## Priority

**Medium** - Improves release reliability and debugging, but current approach works

## Effort

4-6 hours

## Benefits

- Independent service builds - failures don't block other services
- Easier debugging - each workflow has its own logs
- Faster iteration - re-run only failed builds
- Better scalability - adding new services is trivial
- Cleaner cache management - no conflicts
- Future: path-based CI triggers for faster PR feedback
