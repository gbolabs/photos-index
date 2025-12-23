# GitHub Copilot Instructions

This file provides instructions for GitHub Copilot when working with this repository.

> **Note**: GitHub Copilot also reads `CLAUDE.md` from the repository root for additional context.
> See: https://github.blog/changelog/2025-07-23-github-copilot-coding-agent-now-supports-instructions-md-custom-instructions/

## Language Requirements

**Always use English** for all generated content, regardless of the language used in prompts or comments:

- Code comments
- Commit messages
- Pull request titles and descriptions
- Documentation
- Variable, function, and class names
- Log messages and error messages

## Technology Stack

- **Backend**: .NET 10 (ASP.NET Core API, Console apps)
- **Frontend**: Angular 21
- **Database**: PostgreSQL with Entity Framework Core
- **Testing**: xUnit, TestContainers, Playwright

## Code Style

- Follow existing patterns in the codebase
- Use Central Package Management (no versions in csproj files)
- Prefer streaming for large file operations
- Add appropriate error handling at system boundaries

## Commit Messages

Use Conventional Commits format:
- `feat(scope): description` for new features
- `fix(scope): description` for bug fixes
- `docs: description` for documentation
- `test: description` for tests
- `chore: description` for maintenance

## Additional Context

See `CLAUDE.md` for comprehensive project documentation and guidelines.
