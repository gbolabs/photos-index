# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Photo indexing and deduplication application designed for Synology NAS deployment. Scans directories for images, computes SHA256 hashes for deduplication, and provides a web interface for managing duplicates.

## Technology Stack

- **Backend**: .NET 10 (ASP.NET Core API, Console apps)
- **Frontend**: Angular 21
- **Database**: PostgreSQL with EF Core migrations
- **Observability**: Aspire Dashboard (standalone container) for logs, traces, metrics
- **Deployment**: Docker Compose (production/NAS), Podman/Kubernetes (local dev)
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

### Local Development (Podman + Kubernetes)

```bash
# Build all container images
./deploy/kubernetes/local-dev.sh build

# Start all services
PHOTOS_PATH=~/Pictures ./deploy/kubernetes/local-dev.sh start

# Check status
./deploy/kubernetes/local-dev.sh status

# View logs
./deploy/kubernetes/local-dev.sh logs

# Stop all services
./deploy/kubernetes/local-dev.sh stop
```

Access points:
- Web UI: http://localhost:8080
- API: http://localhost:5000
- Aspire Dashboard: http://localhost:18888

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

## Architecture

### Services
1. **Indexing Service**: Scans directories, extracts metadata, computes SHA256 hashes, generates thumbnails
2. **API Service**: REST endpoints for data ingestion, duplicate handling, directory configuration
3. **Database**: PostgreSQL with EF Core, entities: IndexedFiles, ScanDirectories, DuplicateGroups
4. **Web Interface**: Angular app for search, filtering, duplicate management
5. **Cleaner Service**: Safe file removal with soft delete, dry-run, transaction logging

### Observability
- Aspire Dashboard at `http://localhost:18888` receives OpenTelemetry data
- All .NET services configured with OTLP exporter
- Environment: `OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889`

### Key Patterns
- Services communicate via REST API within Docker network
- Configuration via environment variables
- Change detection using file modification timestamps or hashes
- Streaming hash computation for memory efficiency

## Development Guidelines

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
