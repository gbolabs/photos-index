# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Photo indexing and deduplication application designed for Synology NAS deployment. Scans directories for images, computes SHA256 hashes for deduplication, and provides a web interface for managing duplicates.

## Technology Stack

- **Backend**: .NET 10 (ASP.NET Core API, Console apps)
- **Frontend**: Angular 21
- **Database**: PostgreSQL with EF Core migrations
- **Observability**: Aspire Dashboard (standalone container) for logs, traces, metrics
- **Deployment**: Docker Compose (production/NAS), Podman/Kubernetes (local dev), Traefik reverse proxy
- **Testing**: xUnit, TestContainers, Playwright, BenchmarkDotNet

## Project Structure

```
src/
├── PhotosIndex.sln
├── Api/                    # ASP.NET Core REST API
├── IndexingService/        # .NET Console app for file scanning/hashing
├── CleanerService/         # .NET service for safe duplicate removal
├── Database/               # EF Core DbContext, entities, migrations
├── Shared/                 # Shared DTOs and contracts
└── Web/                    # Angular 21 web interface

tests/
├── Api.Tests/
├── IndexingService.Tests/
├── Database.Tests/
├── Integration.Tests/      # TestContainers-based
└── E2E.Tests/              # Playwright

deploy/
├── docker/                 # Docker Compose for Synology NAS
└── kubernetes/             # K8s manifests for local Podman dev
```

## Package Management

Uses NuGet Central Package Management (CPM):
- `Directory.Packages.props` - All package versions defined centrally
- `Directory.Build.props` - Shared build properties
- csproj files reference packages without version attributes

## Build Commands

```bash
# Backend
dotnet restore src/PhotosIndex.sln
dotnet build src/PhotosIndex.sln
dotnet run --project src/Api/Api.csproj
dotnet test src/PhotosIndex.sln

# Frontend
cd src/Web && npm install && ng serve

# EF Core Migrations
dotnet ef migrations add <Name> --project src/Database --startup-project src/Api
dotnet ef database update --project src/Database --startup-project src/Api
```

## Deployment

### Local Development (Podman)

Uses `podman kube play` with a Pod manifest (`deploy/kubernetes/photos-index.yaml`).

```bash
# Build all container images
./deploy/kubernetes/local-dev.sh build

# Start all services (uses podman kube play)
PHOTOS_PATH=~/Pictures ./deploy/kubernetes/local-dev.sh start

# Check status
./deploy/kubernetes/local-dev.sh status

# View logs
./deploy/kubernetes/local-dev.sh logs

# Stop all services
./deploy/kubernetes/local-dev.sh stop
```

Access points (via Traefik):
- Application: http://localhost:8080 (Podman uses 8080 for rootless)
- API: http://localhost:8080/api
- Traefik Dashboard: http://localhost:8081
- Aspire Dashboard: http://localhost:18888
- PostgreSQL: localhost:5432

### Synology NAS (Docker Compose)

```bash
cd deploy/docker

# Configure environment
cp .env.example .env
# Edit .env with your settings

# Start services
docker compose up -d

# View logs
docker compose logs -f

# Stop services
docker compose down
```

## Releasing

**IMPORTANT**: Releases are fully automated via GitHub Actions. Do NOT manually create releases.

### Release Process

1. **Create and push a tag** - this triggers the release workflow:
   ```bash
   git tag -a v0.1.0 -m "Release v0.1.0"
   git push origin v0.1.0
   ```
   Or use `gh release create` which creates the tag automatically.

2. **The release workflow** (`.github/workflows/release.yml`) will:
   - Build container images for all services (api, web, indexing-service, cleaner-service)
   - Push images to `ghcr.io/gbolabs/photos-index/<service>:<version>`
   - Create the GitHub Release with auto-generated changelog

3. **Do NOT**:
   - Manually create GitHub releases before the workflow completes
   - Use `gh release create` with custom release notes (let the workflow generate them)

4. **Monitor the release**:
   ```bash
   gh run list --workflow=release.yml --limit 3
   gh run watch <run-id>
   ```

### Hotfix Releases

For patch releases (e.g., v0.1.1):
1. Fix the issue on `main` via PR
2. After merge, create the patch tag: `git tag v0.1.1 && git push origin v0.1.1`
3. Let the workflow build and release

## Architecture

### Services
1. **Traefik**: Reverse proxy providing single entry point, routes `/` to Web and `/api` to API
2. **Indexing Service**: Scans directories, extracts metadata, computes SHA256 hashes, generates thumbnails
3. **API Service**: REST endpoints for data ingestion, duplicate handling, directory configuration
4. **Database**: PostgreSQL with EF Core, entities: IndexedFiles, ScanDirectories, DuplicateGroups
5. **Web Interface**: Angular app for search, filtering, duplicate management
6. **Cleaner Service**: Safe file removal with soft delete, dry-run, transaction logging

### Observability
- Aspire Dashboard at `http://localhost:18888` receives OpenTelemetry data
- All .NET services configured with OTLP exporter
- Environment: `OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889`

### Key Patterns
- Local dev: All services in one Pod, communicate via localhost
- Docker Compose: Services communicate via Docker network (service names)
- Configuration via environment variables
- Change detection using file modification timestamps or hashes
- Streaming hash computation for memory efficiency

## Development Guidelines

### Language Requirements

**All repository content must be in English**, regardless of the language used to prompt AI assistants (Claude, Copilot, etc.):
- Code comments
- Commit messages
- Pull request titles and descriptions
- Documentation
- Variable/function/class names
- Log messages and error messages

### Test Coverage Requirements
- API: 85% | IndexingService: 80% | CleanerService: 80% | Database: 75% | Web: 70%

### Supported Image Formats
`.jpg`, `.jpeg`, `.png`, `.gif`, `.heic`, `.webp`, `.bmp`, `.tiff` (case-insensitive)

### Resource Constraints
Optimize for Synology NAS - use streaming for large files, pagination for queries, memory limits in Docker.

## Parallel Development

Multiple agents can work simultaneously on different components:
- Agent 1: API + Database (start here, defines contracts)
- Agent 2: Indexing Service (depends on Database schema)
- Agent 3: Angular Web UI (depends on API contracts)
- Agent 4: CI/CD + Docker
- Agent 5: Cleaner Service

Coordinate via shared DTOs in `src/Shared/` and avoid concurrent EF Core migrations.

## Backlog & Task Management

**Task tracking lives in `docs/backlog/`** - check `docs/backlog/README.md` for current status.

### Agent Workflow

1. **Before starting**: Check `docs/backlog/README.md` Status Overview for available tasks
2. **Pick a task**: Select from your assigned track (see Agent Assignment Matrix)
3. **Create branch**: Use the branch name specified in the task file
4. **Implement**: Follow TDD steps and acceptance criteria in the task file
5. **Create PR**: Include descriptive title and body
6. **Update backlog**: **REQUIRED** - before considering work complete:

   a. Update the task's `.md` file header:
   ```markdown
   **Status**: ✅ Complete
   **PR**: [#N](https://github.com/gbolabs/photos-index/pull/N)
   ```

   b. Update `docs/backlog/README.md` Status Overview table:
   - Change status from `🔲 Not Started` to `✅ Complete`
   - Add PR link

### Current Progress

See `docs/backlog/README.md` for the live status of all 18 backlog tasks.
