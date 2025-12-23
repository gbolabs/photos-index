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

## References

- [ADR GitHub Organization](https://adr.github.io/)
- [Michael Nygard's ADR article](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
