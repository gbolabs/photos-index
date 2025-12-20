# Photos Index

A photo indexing and deduplication application designed for Synology NAS deployment. Scans directories for images, computes SHA256 hashes for deduplication, and provides a web interface for managing duplicate files.

## Features

- **Directory Scanning**: Automatically scan configured directories for image files
- **Duplicate Detection**: SHA256 hash-based file deduplication
- **Web Interface**: Angular-based UI for browsing files and managing duplicates
- **Observability**: Full OpenTelemetry integration with Aspire Dashboard
- **Container Ready**: Deploy with Docker Compose or Kubernetes

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/) or [Podman](https://podman.io/)
- PostgreSQL 16+

### Local Development (Podman/Kubernetes)

```bash
# Build container images
./deploy/kubernetes/local-dev.sh build

# Start all services
PHOTOS_PATH=~/Pictures ./deploy/kubernetes/local-dev.sh start

# Check status
./deploy/kubernetes/local-dev.sh status

# View logs
./deploy/kubernetes/local-dev.sh logs

# Stop services
./deploy/kubernetes/local-dev.sh stop
```

### Docker Compose (Synology NAS)

```bash
cd deploy/docker

# Configure environment
cp .env.example .env
# Edit .env with your settings

# Start services
docker compose up -d
```

### Access Points

| Service | URL |
|---------|-----|
| Web UI | http://localhost:8080 |
| API Swagger | http://localhost:8080/api/swagger |
| Aspire Dashboard | http://localhost:18888 |
| Traefik Dashboard | http://localhost:8081 |

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Traefik Ingress                         │
│                      (Reverse Proxy + OTEL)                     │
└─────────────────────────┬───────────────────────────────────────┘
                          │
          ┌───────────────┼───────────────┐
          │               │               │
          ▼               ▼               ▼
    ┌──────────┐   ┌──────────┐   ┌──────────────┐
    │ Web UI   │   │   API    │   │   Indexing   │
    │ (Angular)│   │ (ASP.NET)│   │   Service    │
    └──────────┘   └────┬─────┘   └──────┬───────┘
                        │                │
                        └────────┬───────┘
                                 │
                        ┌────────▼────────┐
                        │   PostgreSQL    │
                        │   (Database)    │
                        └─────────────────┘
```

## Technology Stack

| Component | Technology |
|-----------|------------|
| Backend API | .NET 10 (ASP.NET Core) |
| Frontend | Angular 21 |
| Database | PostgreSQL with EF Core |
| Observability | OpenTelemetry + Aspire Dashboard |
| Reverse Proxy | Traefik v3 |
| Containerization | Docker / Podman |

## Project Structure

```
├── src/
│   ├── Api/                 # REST API (ASP.NET Core)
│   ├── IndexingService/     # File scanning service
│   ├── CleanerService/      # Safe duplicate removal
│   ├── Database/            # EF Core DbContext & migrations
│   ├── Shared/              # Shared DTOs and contracts
│   └── Web/                 # Angular web interface
├── tests/
│   ├── Api.Tests/           # Unit tests
│   ├── Integration.Tests/   # TestContainers integration tests
│   └── E2E.Tests/           # Playwright E2E tests
├── deploy/
│   ├── docker/              # Docker Compose for NAS
│   └── kubernetes/          # K8s manifests for local dev
└── docs/                    # Documentation
```

## Documentation

- [Implementation Plan](docs/implementation-plan.md) - Overall development strategy
- [Features](docs/features.md) - Detailed feature specifications
- [Development Backlog](docs/backlog/README.md) - Task tracking and status
- [Claude Integration](CLAUDE.md) - AI-assisted development guide

## Build Commands

```bash
# Backend
dotnet restore src/PhotosIndex.sln
dotnet build src/PhotosIndex.sln
dotnet test src/PhotosIndex.sln

# Frontend
cd src/Web
npm install
npm run build

# Run API locally
dotnet run --project src/Api/Api.csproj

# Run Angular dev server
cd src/Web && ng serve
```

## Database Migrations

```bash
# Add new migration
dotnet ef migrations add <Name> --project src/Database --startup-project src/Api

# Apply migrations
dotnet ef database update --project src/Database --startup-project src/Api
```

## Testing

```bash
# Unit tests
dotnet test tests/Api.Tests

# Integration tests (requires Docker)
dotnet test tests/Integration.Tests

# E2E tests (requires running application)
cd tests/E2E.Tests
npm install
npx playwright install
npx playwright test
```

## Supported Image Formats

`.jpg`, `.jpeg`, `.png`, `.gif`, `.heic`, `.webp`, `.bmp`, `.tiff`

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [Claude Code](https://claude.ai/code) AI assistance
- Uses [Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/) for observability
- Designed for [Synology NAS](https://www.synology.com/) deployment
