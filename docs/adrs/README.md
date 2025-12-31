# Architecture Decision Records (ADRs)

This directory contains Architecture Decision Records (ADRs) documenting significant technical decisions made in this project.

## What is an ADR?

An ADR is a short document that captures an important architectural decision along with its context and consequences.

## When to Write an ADR

**Do document:**
- Technology choices (frameworks, libraries, databases)
- Architectural patterns (API design, data flow, service boundaries)
- Development workflow decisions (CI/CD, branching strategy)
- Standards and conventions that affect the whole codebase
- Integration decisions (third-party services, protocols)
- Security-related decisions

**Don't document:**
- Bug fixes or minor refactoring
- Implementation details within a single component
- Temporary workarounds (unless they become permanent)
- Decisions that are easily reversible with no impact
- Standard framework usage following official documentation

## How to Create an ADR

1. Copy `000-template.md` to a new file: `NNN-short-title.md`
2. Use the next available number (check existing ADRs)
3. Fill in all sections
4. Submit as part of a PR or as a standalone documentation PR

## ADR Index

| ID | Title | Status | Date |
|----|-------|--------|------|
| [001](./001-copilot-claude-instructions.md) | GitHub Copilot and Claude Code Instructions | Accepted | 2025-12-23 |
| [002](./002-english-language-requirement.md) | English Language Requirement | Accepted | 2025-12-23 |
| [003](./003-remove-devcontainer-support.md) | Remove Dev Container Support | Accepted | 2025-12-23 |
| [004](./004-distributed-processing-architecture.md) | Distributed Processing Architecture | Proposed | 2025-12-24 |
| [005](./005-claude-api-traffic-logging.md) | Claude API Traffic Logging via Proxy Container | Proposed | 2025-12-24 |
| [006](./006-jaeger-over-aspire.md) | Grafana Observability Stack Over Aspire Dashboard | Accepted | 2025-12-25 |
| [007](./007-masstransit-messaging-patterns.md) | MassTransit Messaging Patterns for Distributed Processing | Accepted | 2025-12-25 |
| [008](./008-signalr-indexer-communication.md) | SignalR for API-Indexer Bidirectional Communication | Accepted | 2025-12-26 |
| [009](./009-watchtower-auto-deployment.md) | Watchtower for Automated Container Updates | Accepted | 2025-12-28 |
| [010](./010-image-preview-architecture.md) | Image Preview Architecture | Accepted | 2025-12-28 |
| [011](./011-gallery-view-architecture.md) | Gallery View Architecture | Proposed | 2025-12-29 |
| [012](./012-incremental-indexing.md) | Incremental Indexing with Scan Sessions | Accepted | 2025-12-29 |
| [013](./013-cleaner-service-architecture.md) | Cleaner Service Architecture for Safe Duplicate Removal | Accepted | 2025-12-29 |
| [014](./014-duplicate-group-status-workflow.md) | DuplicateGroup Status Workflow | Accepted | 2025-12-30 |
| [015](./015-authentication-authorization.md) | Authentication and Authorization with External IDP | Proposed | 2025-12-31 |

## References

- [ADR GitHub Organization](https://adr.github.io/)
- [Michael Nygard's ADR article](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
