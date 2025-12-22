# Migrate Backlog to GitHub Issues

## Problem Statement

Backlog items in `docs/backlog/` are not easily discoverable, trackable, or discussable. GitHub Issues provides better project management features.

## Proposed Approach: Hybrid

1. **GitHub Issues** for tracking work items
   - What to do
   - Priority (labels)
   - Status (open/closed)
   - Assignees
   - Linked PRs

2. **docs/backlog/** for detailed specs
   - Architecture decisions
   - Code examples
   - Diagrams
   - Implementation details

## Migration Plan

### Step 1: Create Labels

```bash
gh label create "area:api" --color "0052CC"
gh label create "area:indexer" --color "006B75"
gh label create "area:web" --color "1D76DB"
gh label create "area:infra" --color "5319E7"
gh label create "type:enhancement" --color "A2EEEF"
gh label create "type:bug" --color "D73A4A"
gh label create "type:docs" --color "0075CA"
gh label create "type:performance" --color "FBCA04"
gh label create "priority:high" --color "B60205"
gh label create "priority:medium" --color "FBCA04"
gh label create "priority:low" --color "0E8A16"
```

### Step 2: Create Issues from Backlog

| Backlog File | Issue Title | Labels |
|--------------|-------------|--------|
| 04-cleaner-service/002-smart-duplicate-selection.md | Smart duplicate selection | area:api, type:enhancement |
| 05-performance/001-deferred-thumbnail-generation.md | Deferred thumbnail generation | area:indexer, type:performance, priority:high |
| 05-performance/002-progressive-ingestion.md | ✅ Done in v0.0.3 | - |
| 06-ci-cd/001-container-retention-policy.md | Container image retention policy | area:infra, priority:low |
| 06-ci-cd/002-postgresql-otel-metrics.md | PostgreSQL OTEL metrics | area:infra, priority:low |
| 06-ci-cd/003-release-ci-verification.md | Release CI verification | area:infra, priority:medium |

### Step 3: Link Issues to Specs

Each issue body includes:
```markdown
## Specification

See [detailed spec](docs/backlog/path/to/file.md) for implementation details.
```

### Step 4: Clean Up

- Keep detailed specs in repo
- Delete simple tracking docs after migration
- Update README with link to Issues

## Benefits

- Kanban board via GitHub Projects
- Notifications on updates
- Community contributions
- Automatic PR linking

## Priority

**Low** - Nice to have for project organization

## Effort

2-3 hours
