# ADR-001: GitHub Copilot and Claude Code Instructions

**Status**: Accepted
**Date**: 2025-12-23
**Author**: Claude Opus 4.5

## Context

Multiple AI coding assistants are used in this project:
- **Claude Code**: Anthropic's CLI tool for autonomous coding tasks
- **GitHub Copilot**: IDE-integrated code completion and chat

Each tool has its own configuration mechanism, but without explicit instructions, AI assistants may:
- Use inconsistent coding styles
- Generate content in the wrong language
- Miss project-specific conventions
- Not understand the technology stack

## Decision

Configure both AI assistants with project-specific instructions:

1. **`CLAUDE.md`** (repository root): Primary instructions for Claude Code, containing comprehensive project documentation, build commands, and development guidelines.

2. **`.github/copilot-instructions.md`**: GitHub Copilot-specific instructions. Kept minimal since Copilot also reads `CLAUDE.md` automatically.

3. **Shared instructions**: Both files enforce the same core rules (English language, commit format, code style).

## Consequences

### Positive

- Consistent code generation across AI tools
- AI assistants understand project conventions without repeated prompting
- Reduced errors from AI not knowing the tech stack
- GitHub Copilot coding agent reads both files, getting full context

### Negative

- Two files to maintain (though copilot-instructions.md is minimal)
- Must update instructions when project conventions change

## References

- [GitHub Copilot .instructions.md support](https://github.blog/changelog/2025-07-23-github-copilot-coding-agent-now-supports-instructions-md-custom-instructions/)
- [Claude Code documentation](https://docs.anthropic.com/en/docs/claude-code)
- PR: #86
