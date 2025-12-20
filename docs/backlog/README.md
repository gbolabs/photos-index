# Implementation Backlog

Ordered backlog for Photos Index application. Designed for parallel agent development with clear dependencies.

## Agent Assignment Matrix

| Agent | Track | Dependencies | Branch Prefix |
|-------|-------|--------------|---------------|
| A1 | Shared Contracts + API Core | None | `feature/api-` |
| A2 | Indexing Service | Shared DTOs (A1 first task) | `feature/indexing-` |
| A3 | Cleaner Service | Shared DTOs (A1 first task) | `feature/cleaner-` |
| A4 | Web UI | API Contracts (A1 tasks 1-2) | `feature/web-` |
| A5 | Integration Tests | API + Services (A1-A3) | `feature/integration-` |
| A6 | E2E Tests | Web UI (A4) | `feature/e2e-` |

## Parallelization Diagram

```
                    ┌─────────────────┐
                    │ 01-001 DTOs     │ ← START HERE (A1)
                    │ (Shared)        │
                    └────────┬────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
        ▼                    ▼                    ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│ 02-* API      │   │ 03-* Indexing │   │ 04-* Cleaner  │
│ (A1 continues)│   │ (A2)          │   │ (A3)          │
└───────┬───────┘   └───────┬───────┘   └───────┬───────┘
        │                   │                   │
        └─────────┬─────────┴─────────┬─────────┘
                  │                   │
                  ▼                   ▼
          ┌───────────────┐   ┌───────────────┐
          │ 05-* Web UI   │   │ 06-* Integr.  │
          │ (A4)          │   │ (A5)          │
          └───────┬───────┘   └───────────────┘
                  │
                  ▼
          ┌───────────────┐
          │ 07-* E2E      │
          │ (A6)          │
          └───────────────┘
```

## Priority Order

### P0 - Critical Path (Week 1)
1. `01-shared-contracts/001-dtos.md` - Unlocks all other work
2. `02-api-layer/001-scan-directories.md` - Core CRUD
3. `02-api-layer/002-indexed-files.md` - Main data API
4. `03-indexing-service/001-file-scanner.md` - Core functionality

### P1 - Core Features (Week 1-2)
5. `03-indexing-service/002-hash-computer.md`
6. `03-indexing-service/003-metadata-extractor.md`
7. `02-api-layer/003-duplicate-groups.md`
8. `04-cleaner-service/001-delete-manager.md`

### P2 - User Interface (Week 2)
9. `05-web-ui/001-api-services.md`
10. `05-web-ui/002-dashboard.md`
11. `05-web-ui/003-directory-settings.md`
12. `05-web-ui/004-file-browser.md`
13. `05-web-ui/005-duplicate-viewer.md`

### P3 - Quality Assurance (Week 2-3)
14. `06-integration/001-api-integration-tests.md`
15. `06-integration/002-service-integration-tests.md`
16. `07-e2e-testing/001-playwright-setup.md`
17. `07-e2e-testing/002-user-workflows.md`

## Branch & PR Rules

1. Each task = one feature branch from `main`
2. All tests must pass before PR
3. Coverage thresholds enforced per CLAUDE.md
4. No concurrent EF Core migrations
5. Shared DTOs changes require coordination

## Task File Format

Each `.md` file contains:
- **Objective**: What to build
- **Dependencies**: What must be complete first
- **Acceptance Criteria**: Definition of done
- **TDD Steps**: Red-Green-Refactor sequence
- **Files to Create/Modify**: Explicit file list
- **Test Coverage**: Minimum requirements
