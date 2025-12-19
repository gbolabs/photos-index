# Implementation Plan for Picture File Indexing and Deduplication

This document outlines the implementation plan for the Docker-based picture file indexing and deduplication application.

## Project Overview

### Objectives
- Create a containerized application to index picture files
- Identify and remove duplicate files efficiently
- Provide web interface for duplicate management
- Optimize for Synology NAS deployment

### Technology Stack
- **Backend**: .NET 10 (ASP.NET Core, Console apps)
- **Frontend**: Angular 21
- **Database**: PostgreSQL with EF Core migrations
- **Observability**: Aspire Dashboard (standalone) for logs, traces, metrics
- **Deployment**: Docker Compose (Synology NAS), Podman/Kubernetes (local dev)
- **Testing**: xUnit, TestContainers, Playwright, BenchmarkDotNet

## Project Structure

```
photos-index/
├── .github/
│   └── workflows/              # GitHub Actions CI/CD
├── docs/                       # Documentation
├── src/
│   ├── PhotosIndex.sln         # Solution file
│   ├── Api/                    # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   ├── Services/
│   │   └── Api.csproj
│   ├── IndexingService/        # .NET Console app
│   │   ├── Workers/
│   │   ├── FileProcessors/
│   │   └── IndexingService.csproj
│   ├── CleanerService/         # .NET Console app
│   │   └── CleanerService.csproj
│   ├── Database/               # EF Core DbContext & migrations
│   │   ├── Entities/
│   │   ├── Migrations/
│   │   └── Database.csproj
│   ├── Shared/                 # Shared DTOs, contracts
│   │   └── Shared.csproj
│   └── Web/                    # Angular 21 app
│       ├── src/
│       └── angular.json
├── tests/
│   ├── Api.Tests/
│   ├── IndexingService.Tests/
│   ├── Database.Tests/
│   ├── Integration.Tests/      # TestContainers-based
│   └── E2E.Tests/              # Playwright
├── deploy/
│   ├── docker/
│   │   ├── docker-compose.yml          # Production (Synology)
│   │   ├── docker-compose.override.yml # Dev overrides
│   │   └── */Dockerfile
│   └── kubernetes/             # Local Podman dev
│       ├── photos-index.yaml   # Pod manifest for podman kube play
│       └── local-dev.sh        # Helper script
└── CLAUDE.md
```

## Build & Run Commands

### Prerequisites
```bash
# Required
dotnet --version    # .NET 10 SDK
node --version      # Node.js 24+
docker --version    # Docker or Podman
```

### Backend (.NET)
```bash
# Restore and build entire solution
dotnet restore src/PhotosIndex.sln
dotnet build src/PhotosIndex.sln

# Run API
dotnet run --project src/Api/Api.csproj

# Run Indexing Service
dotnet run --project src/IndexingService/IndexingService.csproj

# Run all tests
dotnet test src/PhotosIndex.sln

# Run specific test project
dotnet test tests/Api.Tests/Api.Tests.csproj

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

### Frontend (Angular)
```bash
cd src/Web

# Install dependencies
npm install

# Development server
ng serve

# Build for production
ng build --configuration production

# Run tests
ng test

# Run tests headless (CI)
ng test --watch=false --browsers=ChromeHeadless
```

### Database (EF Core)
```bash
# Add migration
dotnet ef migrations add <MigrationName> --project src/Database --startup-project src/Api

# Update database
dotnet ef database update --project src/Database --startup-project src/Api

# Generate SQL script
dotnet ef migrations script --project src/Database --startup-project src/Api
```

### Docker Compose (Synology NAS)
```bash
cd deploy/docker

# Start all services (development)
docker compose up -d

# Start with rebuild
docker compose up -d --build

# View logs
docker compose logs -f

# Stop all services
docker compose down

# Stop and remove volumes (clean slate)
docker compose down -v
```

### Podman (Local Development)
Uses `podman kube play` with a single Pod manifest. All services run in one pod and communicate via localhost.

```bash
cd deploy/kubernetes

# Build all container images
./local-dev.sh build

# Start all services
PHOTOS_PATH=~/Pictures ./local-dev.sh start

# Check status
./local-dev.sh status

# View logs
./local-dev.sh logs

# Stop all services
./local-dev.sh stop
```

**WSL2 Note**: For Windows access, enable mirrored networking in `%USERPROFILE%\.wslconfig`:
```ini
[wsl2]
networkingMode=mirrored
```

## Implementation Phases

### Phase 1: Foundation
**Goal**: Project scaffolding, CI/CD, basic infrastructure

**Tasks**:
1. Create .NET solution structure with all projects
2. Set up Angular 21 app with Angular Material
3. Configure EF Core with PostgreSQL, create initial schema
4. Set up Docker Compose with:
   - PostgreSQL container
   - Aspire Dashboard container (observability)
   - Service containers
5. Create GitHub Actions workflow for CI
6. Configure OpenTelemetry in all .NET services

**Database Schema (Initial)**:
```
IndexedFiles
├── Id (GUID, PK)
├── FilePath (string, indexed)
├── FileName (string)
├── FileSize (long)
├── FileHash (string, indexed)  -- SHA256
├── CreatedAt (DateTime)
├── ModifiedAt (DateTime)
├── IndexedAt (DateTime)
├── Width (int?)
├── Height (int?)
├── ThumbnailPath (string?)
└── IsDuplicate (bool, indexed)

ScanDirectories
├── Id (GUID, PK)
├── Path (string)
├── IsEnabled (bool)
├── LastScannedAt (DateTime?)
└── CreatedAt (DateTime)

DuplicateGroups
├── Id (GUID, PK)
├── Hash (string, indexed)
├── FileCount (int)
└── ResolvedAt (DateTime?)
```

### Phase 2: Core Services
**Goal**: Working indexing and API services

**Indexing Service Tasks**:
1. Directory scanning with configurable file extensions
2. SHA256 hash computation with streaming (memory-efficient)
3. Metadata extraction (dimensions, EXIF dates)
4. Thumbnail generation using ImageSharp
5. Progress reporting via API
6. Change detection (skip unchanged files)

**API Service Tasks**:
1. CRUD endpoints for ScanDirectories
2. File ingestion endpoint (batch support)
3. Duplicate detection queries
4. Pagination for file listings
5. Health check endpoint
6. Progress/status endpoint for indexing service

**Supported Image Formats**:
`.jpg`, `.jpeg`, `.png`, `.gif`, `.heic`, `.webp`, `.bmp`, `.tiff` (case-insensitive)

### Phase 3: Web Interface
**Goal**: Functional Angular UI for duplicate management

**Tasks**:
1. Dashboard with statistics (total files, duplicates, storage saved)
2. Directory configuration page
3. File browser with thumbnails, search, filters
4. Duplicate groups view with side-by-side comparison
5. Bulk selection and actions
6. Indexing progress visualization
7. Responsive layout for desktop/tablet

### Phase 4: Cleaner Service
**Goal**: Safe duplicate removal with safeguards

**Tasks**:
1. Confirmation workflow (mark for deletion → review → execute)
2. Soft delete first (move to trash directory)
3. Transaction logging for all deletions
4. Dry-run mode
5. Rollback capability from logs
6. API endpoints for cleanup operations

### Phase 5: Integration & Polish
**Goal**: Production-ready deployment

**Tasks**:
1. End-to-end testing with Playwright
2. Performance testing with large directories (10k+ files)
3. Memory profiling and optimization
4. Docker image optimization (multi-stage builds)
5. Documentation and user guide
6. Synology NAS deployment testing

## Observability with Aspire Dashboard

### Docker Compose Configuration
```yaml
aspire-dashboard:
  image: mcr.microsoft.com/dotnet/aspire-dashboard:9.1
  ports:
    - "18888:18888"   # Dashboard UI
    - "4317:18889"    # OTLP gRPC endpoint
  environment:
    - DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true
```

### Service Configuration
All .NET services will include OpenTelemetry:
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(logging => logging
    .AddOtlpExporter());
```

Environment variables for services:
```
OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
OTEL_SERVICE_NAME=<service-name>
```

### Future: Persistent Logging
For production on Synology, can add Seq or Loki+Grafana for persistent log storage. Aspire Dashboard remains useful for real-time debugging.

## Testing Strategy

### Coverage Requirements
| Component | Minimum Coverage |
|-----------|-----------------|
| API | 85% |
| IndexingService | 80% |
| CleanerService | 80% |
| Database | 75% |
| Web (Angular) | 70% |

### Test Types
1. **Unit Tests**: Individual classes and methods
2. **Integration Tests**: Service + database using TestContainers
3. **E2E Tests**: Full workflow with Playwright
4. **Performance Tests**: BenchmarkDotNet for critical paths

### CI Pipeline
```yaml
# .github/workflows/ci.yml
- Build all projects
- Run unit tests with coverage
- Run integration tests (TestContainers)
- Build Docker images
- Run E2E tests against containers
- Report coverage to Codecov
- Security scan with Trivy
```

## Parallel Development

The modular structure supports multiple agents working simultaneously:

| Agent | Focus Area | Dependencies |
|-------|------------|--------------|
| Agent 1 | API + Database | None (start here) |
| Agent 2 | Indexing Service | Database schema |
| Agent 3 | Angular Web UI | API contracts |
| Agent 4 | CI/CD + Docker | Project structure |
| Agent 5 | Cleaner Service | API + Database |

**Coordination Points**:
- Shared DTOs in `src/Shared/`
- API contracts defined early
- Database migrations managed by one agent at a time

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Large file sets cause OOM | Streaming hash computation, pagination |
| HEIC format issues | Use ImageSharp with HEIC plugin |
| Synology resource limits | Test on actual NAS early, set memory limits |
| Duplicate false positives | SHA256 + file size comparison |
| Accidental file deletion | Soft delete, dry-run mode, transaction logs |

## Success Criteria

- [ ] Index 10,000+ photos without memory issues
- [ ] Correctly identify all duplicates (same hash)
- [ ] Web UI responsive and usable
- [ ] Clean deployment on Synology NAS via Docker Compose
- [ ] All tests passing, coverage thresholds met
- [ ] Logs visible in Aspire Dashboard
