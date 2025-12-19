# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Photo indexing and deduplication application designed for Synology NAS deployment. Scans directories for images, computes hashes for deduplication, and provides a web interface for managing duplicates.

## Technology Stack

- **Backend**: .NET 10 (ASP.NET Core API, Console apps)
- **Frontend**: Angular 21
- **Database**: PostgreSQL
- **Orchestration**: .NET Aspire
- **Deployment**: Docker Compose (production/NAS), Kubernetes/Podman (local dev)
- **Testing**: xUnit, TestContainers, Playwright, BenchmarkDotNet

## Project Structure

```
src/
├── api/                    # ASP.NET Core REST API service
├── indexing-service/       # .NET Console app for file scanning/hashing
├── cleaner-service/        # .NET service for safe duplicate removal
├── web/                    # Angular 21 web interface
├── database/               # PostgreSQL schema and migrations
├── shared/                 # Shared libraries
└── integration-tests/      # Cross-service integration tests

deploy/
├── docker/                 # Docker Compose for Synology NAS
├── kubernetes/             # K8s manifests for local Podman dev
└── azure-pipelines/        # CI/CD pipelines

tests/
├── integration/            # Service-to-service tests (xUnit + TestContainers)
├── e2e/                    # End-to-end tests (Playwright)
└── performance/            # BenchmarkDotNet performance tests
```

## Architecture

### Services
1. **Indexing Service**: Scans directories, extracts metadata, computes file hashes, reports progress to API
2. **API Service**: REST endpoints for data ingestion, duplicate handling, directory configuration, health checks
3. **Database Service**: PostgreSQL with optimized indexing for queried fields
4. **Web Interface**: Angular app for search, filtering, duplicate management, progress visualization
5. **Cleaner Service**: Safe file removal with confirmation workflow, backup logging

### Key Patterns
- Services communicate via REST API within Docker network
- Configuration via environment variables
- Change detection using file modification timestamps or hashes
- Test-first development with 80% coverage minimum for backend, 70% for frontend

## Development Guidelines

### Test Coverage Requirements
- Core Services: 80% minimum
- API Service: 85% minimum
- Web Interface: 70% minimum
- Integration Tests: 90% coverage of critical paths

### Supported Image Formats
`.jpg`, `.jpeg`, `.png`, `.gif`, `.heic` (case-insensitive)

### Resource Constraints
Optimize for Synology NAS deployment - memory-efficient processing for large directories with streaming where possible.
