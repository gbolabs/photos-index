# Implementation Backlog

Ordered backlog for Photos Index application. Designed for parallel agent development with clear dependencies.

## Status Overview

| Task | Status | PR | Agent |
|------|--------|-----|-------|
| `01-001` Shared DTOs | ✅ Complete | [#3](https://github.com/gbolabs/photos-index/pull/3) | A1 |
| `02-001` Scan Directories API | ✅ Complete | [#4](https://github.com/gbolabs/photos-index/pull/4) | A1 |
| `02-002` Indexed Files API | ✅ Complete | [#6](https://github.com/gbolabs/photos-index/pull/6) | A1 |
| `02-003` Duplicate Groups API | ✅ Complete | [#6](https://github.com/gbolabs/photos-index/pull/6) | A1 |
| `02-004` API Versioning | 🔲 Not Started | - | A1 |
| `03-001` File Scanner | ✅ Complete | [#5](https://github.com/gbolabs/photos-index/pull/5) | A2 |
| `03-002` Hash Computer | 🔲 Not Started | - | A2 |
| `03-003` Metadata Extractor | 🔲 Not Started | - | A2 |
| `03-004` Indexing Worker | ✅ Complete | [#9](https://github.com/gbolabs/photos-index/pull/9) | A2 |
| `04-001` Delete Manager | 🔲 Not Started | - | A3 |
| `05-001` Angular API Services | ✅ Complete | [#8](https://github.com/gbolabs/photos-index/pull/8) | A4 |
| `05-002` Dashboard | 🔲 Not Started | - | A4 |
| `05-003` Directory Settings | 🔲 Not Started | - | A4 |
| `05-004` File Browser | 🔲 Not Started | - | A4 |
| `05-005` Duplicate Viewer | ✅ Complete | [#10](https://github.com/gbolabs/photos-index/pull/10) | A4 |
| `06-001` API Integration Tests | ✅ Complete | [#18](https://github.com/gbolabs/photos-index/pull/18) | A5 |
| `06-002` Service Integration Tests | 🔲 Not Started | - | A5 |
| `06-003` Distributed Service Tests | 🔲 Not Started | - | A5 |
| `07-001` Playwright Setup | ✅ Complete | [#16](https://github.com/gbolabs/photos-index/pull/16) | A6 |
| `07-002` User Workflows | 🔲 Not Started | - | A6 |
| `07-003` Playwright CI Integration | ✅ Complete | [#87](https://github.com/gbolabs/photos-index/pull/87) | A6 |
| `06-008` Claude API Traffic Logging | 🔲 Not Started | - | - |
| `06-009` Parallel Release Pipelines | 🔲 Not Started | - | - |
| `08-001` Swagger in Production | ✅ Complete | [#14](https://github.com/gbolabs/photos-index/pull/14) | A1 |
| `08-002` API TraceId in Responses | ✅ Complete | [#14](https://github.com/gbolabs/photos-index/pull/14) | A1 |
| `08-003` Traefik OTEL Integration | ✅ Complete | [#14](https://github.com/gbolabs/photos-index/pull/14) | A4 |
| `08-004` Aspire Health Check | ✅ Complete | [#14](https://github.com/gbolabs/photos-index/pull/14) | A4 |
| `08-005` Angular OTEL Integration | ✅ Complete | [#14](https://github.com/gbolabs/photos-index/pull/14) | A4 |
| `09-001` API Client Alignment | 🔧 In Progress | - | A2 |
| `09-002` Angular API Client Generation | 🔲 Not Started | - | A4 |
| `09-003` Service Bus Communication | 🔲 Not Started | - | A2/A3 |
| `09-004` Thumbnail Offload to MPC | 🔲 Not Started | - | A1/A2 |
| `03-004` Indexing Performance Optimizations | 🔲 Not Started | - | A2 |
| `05-006` File Detail Page | 🔲 Not Started | - | A4 |
| `05-007` Tile View with Details | 🔲 Not Started | - | A4 |
| `03-005` Camera Model Lookup | 🔲 Not Started | - | A2 |
| `11-001` TrueNAS + Synology Split | 🔲 Not Started | - | A4 |
| `12-001` Duplicate Table View | ✅ Complete | [#71](https://github.com/gbolabs/photos-index/pull/71) | A4 |
| `12-002` Batch Validation & Undo | ✅ Complete | [#72](https://github.com/gbolabs/photos-index/pull/72) | A1/A4 |
| `12-003` Selection Algorithm | ✅ Complete | [#74](https://github.com/gbolabs/photos-index/pull/74) | A1/A4 |
| `12-004` Bulk Override by Pattern | 🔲 Not Started | - | A1/A4 |
| `12-005` Cleanup History View | 🔲 Not Started | - | A1/A3/A4 |
| `02-006` Reprocess Files Endpoint | ✅ Complete | [#110](https://github.com/gbolabs/photos-index/pull/110) | A1/A2/A4 |
| `10-001` Real-time Scan Communication | ✅ Complete | [#110](https://github.com/gbolabs/photos-index/pull/110) | A2/A4 |

**Infrastructure & Developer Experience:**
| Task | Status | PR |
|------|--------|-----|
| Traefik Ingress | ✅ Complete | [#11](https://github.com/gbolabs/photos-index/pull/11) |
| Claude Container Enhancement | ✅ Complete | [#7](https://github.com/gbolabs/photos-index/pull/7) |

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
5. `02-api-layer/004-api-versioning.md` - Independent service deployments (v0.2.0)
6. `03-indexing-service/002-hash-computer.md`
7. `03-indexing-service/003-metadata-extractor.md`
8. `02-api-layer/003-duplicate-groups.md`
9. `04-cleaner-service/001-delete-manager.md`

### P1.5 - Observability (Parallel)
- `08-observability/001-swagger-production.md` (A1)
- `08-observability/002-api-trace-id.md` (A1)
- `08-observability/003-traefik-otel.md` (A4)
- `08-observability/004-aspire-health.md` (A4)
- `08-observability/005-angular-otel.md` (A4)

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

### P4 - Performance & UX Enhancements
18. `09-thumbnail-offload/README.md` - Offload thumbnail generation to MPC
19. `05-web-ui/006-file-detail-page.md` - File detail page with extended metadata
20. `09-fixes/003-service-bus-communication.md` - Generic message bus for async ops

## Branch & PR Rules

1. Each task = one feature branch from `main`
2. All tests must pass before PR
3. Coverage thresholds enforced per CLAUDE.md
4. No concurrent EF Core migrations
5. Shared DTOs changes require coordination

## Agent Completion Protocol

**When completing a task, agents MUST:**

1. Create PR with descriptive title and body
2. Update the task's `.md` file:
   - Add `**Status**: ✅ Complete` at the top
   - Add `**PR**: [#N](link)` with PR link
   - Check off all completion checklist items
3. Update `docs/backlog/README.md`:
   - Update status in the Status Overview table
   - Add PR link

Example header for completed task:
```markdown
# 001: Task Name

**Status**: ✅ Complete
**PR**: [#8](https://github.com/gbolabs/photos-index/pull/8)
**Priority**: P2
...
```

## Task File Format

Each `.md` file contains:
- **Objective**: What to build
- **Dependencies**: What must be complete first
- **Acceptance Criteria**: Definition of done
- **TDD Steps**: Red-Green-Refactor sequence
- **Files to Create/Modify**: Explicit file list
- **Test Coverage**: Minimum requirements
