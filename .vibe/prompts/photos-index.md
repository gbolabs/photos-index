# Photos Index Project Instructions

You are working on Photos Index, a photo indexing and deduplication application designed for Synology NAS deployment.

## Technology Stack

- **Backend**: .NET 10 (ASP.NET Core API, Console apps)
- **Frontend**: Angular 21
- **Database**: PostgreSQL with EF Core migrations
- **Observability**: Aspire Dashboard for logs, traces, metrics
- **Deployment**: Docker Compose (production), Podman (local dev), Traefik reverse proxy
- **Testing**: xUnit, TestContainers, Playwright

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

# Full stack (Podman)
PHOTOS_PATH=~/Pictures ./deploy/kubernetes/local-dev.sh start
```

## Key Guidelines

### Language Requirements
**All repository content must be in English** - code comments, commit messages, PR descriptions, documentation, variable names, log messages.

### Test Coverage Requirements
- API: 85% | IndexingService: 80% | CleanerService: 80% | Database: 75% | Web: 70%

### Supported Image Formats
`.jpg`, `.jpeg`, `.png`, `.gif`, `.heic`, `.webp`, `.bmp`, `.tiff` (case-insensitive)

### Resource Constraints
Optimize for Synology NAS - use streaming for large files, pagination for queries, memory limits in Docker.

### Package Management
Uses NuGet Central Package Management (CPM):
- `Directory.Packages.props` - All package versions defined centrally
- `Directory.Build.props` - Shared build properties

## Architecture

### Services
1. **Traefik**: Reverse proxy, routes `/` to Web and `/api` to API
2. **Indexing Service**: Scans directories, extracts metadata, computes SHA256 hashes
3. **API Service**: REST endpoints for data ingestion, duplicate handling
4. **Database**: PostgreSQL with EF Core
5. **Web Interface**: Angular app for duplicate management
6. **Cleaner Service**: Safe file removal with soft delete

### Key Patterns
- Configuration via environment variables
- Change detection using file modification timestamps or hashes
- Streaming hash computation for memory efficiency
- OpenTelemetry for observability

## Development Workflow

1. Check `docs/backlog/README.md` for available tasks
2. Create feature branch from `main`
3. Follow TDD - write tests first
4. Create PR with descriptive title
5. Update backlog status after merge

## Releases

Releases are automated via GitHub Actions:
```bash
git tag v0.x.x && git push origin v0.x.x
```
This builds and pushes container images to `ghcr.io/gbolabs/photos-index/`.
