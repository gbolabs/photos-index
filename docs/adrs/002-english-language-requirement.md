# ADR-002: English Language Requirement

**Status**: Accepted
**Date**: 2025-12-23
**Author**: Claude Opus 4.5

## Context

Contributors and AI assistants may use different languages when:
- Writing code comments
- Creating commit messages
- Documenting features
- Naming variables and functions

This creates inconsistency in the codebase, especially when AI assistants respond in the same language as the user's prompt.

## Decision

Enforce English as the sole language for all repository content, regardless of:
- The contributor's native language
- The language used to prompt AI assistants (Claude, Copilot, etc.)

This applies to:
- Code comments
- Commit messages
- Pull request titles and descriptions
- Issue titles and descriptions
- Documentation
- Variable, function, and class names
- Log messages and error messages

The requirement is documented in:
- `CLAUDE.md` (Development Guidelines)
- `CONTRIBUTING.md` (Language Requirements section)
- `.github/copilot-instructions.md`
- `.github/PULL_REQUEST_TEMPLATE.md` (checklist item)

## Consequences

### Positive

- Consistent codebase language
- Accessible to international contributors
- AI assistants produce consistent output
- Easier code review and maintenance

### Negative

- Non-English speakers must translate their contributions
- AI assistants need explicit reminders to override prompt language

## References

- PR: #86
