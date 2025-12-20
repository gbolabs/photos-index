# Contributing to Photos Index

Thank you for your interest in contributing to Photos Index! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [How to Contribute](#how-to-contribute)
- [Pull Request Process](#pull-request-process)
- [Coding Standards](#coding-standards)
- [Testing Requirements](#testing-requirements)
- [Documentation](#documentation)

## Code of Conduct

This project adheres to a code of conduct. By participating, you are expected to uphold this code:

- Be respectful and inclusive
- Welcome newcomers and help them get started
- Focus on constructive feedback
- Accept responsibility for mistakes and learn from them

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/photos-index.git
   cd photos-index
   ```
3. **Add the upstream remote**:
   ```bash
   git remote add upstream https://github.com/gbolabs/photos-index.git
   ```

## Development Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/) or [Podman](https://podman.io/)
- PostgreSQL 16+ (or use containers)

### Backend Setup

```bash
# Restore dependencies
dotnet restore src/PhotosIndex.sln

# Build the solution
dotnet build src/PhotosIndex.sln

# Run tests
dotnet test src/PhotosIndex.sln
```

### Frontend Setup

```bash
cd src/Web

# Install dependencies
npm install

# Start development server
ng serve
```

### Full Stack Development

```bash
# Start all services with Podman
PHOTOS_PATH=~/Pictures ./deploy/kubernetes/local-dev.sh start
```

## How to Contribute

### Reporting Bugs

1. Check existing [issues](https://github.com/gbolabs/photos-index/issues) first
2. Create a new issue with:
   - Clear, descriptive title
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (OS, .NET version, browser)
   - Screenshots if applicable

### Suggesting Features

1. Check the [backlog](docs/backlog/README.md) for planned features
2. Open a feature request issue with:
   - Clear description of the feature
   - Use case / motivation
   - Proposed implementation (optional)

### Contributing Code

1. Check the [backlog](docs/backlog/README.md) for available tasks
2. Comment on an issue to claim it
3. Create a feature branch from `main`
4. Implement your changes following our standards
5. Submit a pull request

## Pull Request Process

### Branch Naming

Use descriptive branch names:
- `feature/add-thumbnail-generation`
- `fix/duplicate-detection-bug`
- `docs/update-api-documentation`
- `refactor/optimize-hash-computation`

### Commit Messages

Follow conventional commit format:
```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

Example:
```
feat(api): add batch file ingestion endpoint

Implements POST /api/files/batch for ingesting multiple files
in a single request. Includes duplicate detection and validation.

Closes #42
```

### PR Requirements

1. **Create from latest main**:
   ```bash
   git fetch upstream
   git checkout -b feature/my-feature upstream/main
   ```

2. **Write meaningful commits** - Squash WIP commits before submitting

3. **Update documentation** - If your changes affect usage or APIs

4. **Add tests** - All new features must have tests

5. **Pass all checks**:
   - All tests pass
   - Code builds without errors
   - Linting passes

6. **PR Description** should include:
   - Summary of changes
   - Related issue number
   - Testing performed
   - Screenshots (for UI changes)

### Review Process

1. PRs require at least one approval
2. Address all review comments
3. Keep PRs focused - one feature/fix per PR
4. Respond to feedback promptly

## Coding Standards

### C# (.NET)

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use nullable reference types
- Use async/await for I/O operations
- Document public APIs with XML comments

### TypeScript (Angular)

- Follow [Angular Style Guide](https://angular.io/guide/styleguide)
- Use strict TypeScript configuration
- Prefer observables for async operations
- Use standalone components

### General

- Keep methods small and focused
- Write self-documenting code
- Avoid premature optimization
- Handle errors appropriately

## Testing Requirements

### Coverage Thresholds

| Component | Minimum Coverage |
|-----------|-----------------|
| API | 85% |
| IndexingService | 80% |
| CleanerService | 80% |
| Database | 75% |
| Web | 70% |

### Test Types

1. **Unit Tests** - Test individual components in isolation
2. **Integration Tests** - Test API endpoints with real database (TestContainers)
3. **E2E Tests** - Test complete user workflows (Playwright)

### Running Tests

```bash
# All tests
dotnet test src/PhotosIndex.sln

# Specific project
dotnet test tests/Api.Tests

# Integration tests (requires Docker)
dotnet test tests/Integration.Tests

# E2E tests
cd tests/E2E.Tests && npx playwright test
```

## Documentation

### When to Update Docs

- New features or API endpoints
- Changed behavior or configuration
- Bug fixes that affect usage
- Deprecated features

### Documentation Locations

| Type | Location |
|------|----------|
| API Reference | Swagger (auto-generated) |
| Architecture | `docs/` |
| Development | `CLAUDE.md` |
| Task Tracking | `docs/backlog/` |

## Questions?

- Open a [discussion](https://github.com/gbolabs/photos-index/discussions)
- Check existing [documentation](docs/)
- Review the [backlog](docs/backlog/README.md)

Thank you for contributing!
