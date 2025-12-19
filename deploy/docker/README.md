# Docker Deployment for Photos Index

This directory contains Docker Compose configuration and Dockerfiles for deploying the Photos Index application.

## Quick Start

### Prerequisites

- Docker 24.0+ or Docker Desktop
- Docker Compose 2.20+
- At least 2GB of free RAM
- Directory with photos to index

### Basic Setup

1. **Copy environment file:**
   ```bash
   cp .env.example .env
   ```

2. **Edit `.env` file:**
   - Set `HOST_PHOTOS_DIRECTORY` to your photos directory
   - Change `POSTGRES_PASSWORD` to a secure password
   - Adjust other settings as needed

3. **Start all services:**
   ```bash
   docker compose up -d
   ```

4. **Access the application:**
   - Web Interface: http://localhost:4200
   - API: http://localhost:5000
   - Aspire Dashboard: http://localhost:18888
   - PostgreSQL: localhost:5432

5. **View logs:**
   ```bash
   # All services
   docker compose logs -f

   # Specific service
   docker compose logs -f api
   docker compose logs -f indexing-service
   ```

## Services Overview

### PostgreSQL Database
- **Image:** postgres:16-alpine
- **Port:** 5432
- **Volume:** postgres_data (persistent storage)
- **Health Check:** Built-in readiness check

### Aspire Dashboard
- **Image:** mcr.microsoft.com/dotnet/aspire-dashboard:9.1
- **UI Port:** 18888
- **OTLP Port:** 4317
- **Purpose:** Observability (logs, traces, metrics)

### API Service
- **Build:** Multi-stage .NET 9.0 Dockerfile
- **Port:** 5000 (host) -> 8080 (container)
- **Dependencies:** PostgreSQL, Aspire Dashboard
- **Health Check:** /health endpoint

### Indexing Service
- **Build:** Multi-stage .NET 9.0 Dockerfile
- **Dependencies:** API, PostgreSQL
- **Volumes:**
  - Photos directory (read-only)
  - Thumbnails (persistent)

### Cleaner Service
- **Build:** Multi-stage .NET 9.0 Dockerfile
- **Dependencies:** API, PostgreSQL
- **Volumes:**
  - Photos directory (read-write for deletion)
  - Logs (persistent)
- **Note:** Disabled by default (set CLEANER_ENABLED=true to enable)

### Web Interface
- **Build:** Multi-stage Node.js 22 + nginx Dockerfile
- **Port:** 4200 (host) -> 80 (container)
- **Dependencies:** API
- **Features:** SPA routing, gzip compression, security headers

## Development Mode

For development with hot reload:

```bash
# Start with development overrides
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d

# Or simply (docker compose automatically includes override file)
docker compose up -d
```

Development features:
- Source code mounted as volumes
- `dotnet watch` for .NET services
- Angular dev server with hot reload
- PostgreSQL data in local directory

## Production Deployment

### Synology NAS

1. **Enable Container Manager** (formerly Docker package)

2. **Create project folder:**
   ```
   /volume1/docker/photos-index/
   ```

3. **Copy files:**
   - docker-compose.yml
   - .env.example (rename to .env)

4. **Edit .env:**
   ```env
   HOST_PHOTOS_DIRECTORY=/volume1/photos
   ASPNETCORE_ENVIRONMENT=Production
   POSTGRES_PASSWORD=<strong-password>
   INDEXING_INTERVAL_MINUTES=1440  # Once daily
   API_URL=http://<nas-ip>:5000
   ```

5. **Deploy via Container Manager UI:**
   - Import docker-compose.yml
   - Configure environment variables
   - Start services

6. **Or deploy via SSH:**
   ```bash
   cd /volume1/docker/photos-index
   docker compose up -d
   ```

### Other Platforms

The Docker Compose configuration works on:
- Linux servers
- Windows Server with Docker
- macOS with Docker Desktop
- Cloud platforms (AWS, Azure, GCP)

## Common Operations

### Rebuild Services

```bash
# Rebuild specific service
docker compose up -d --build api

# Rebuild all services
docker compose up -d --build
```

### Update Images

```bash
# Pull latest base images
docker compose pull

# Restart with new images
docker compose up -d
```

### Backup Database

```bash
# Export database
docker compose exec postgres pg_dump -U photosuser photosindex > backup.sql

# Restore database
cat backup.sql | docker compose exec -T postgres psql -U photosuser photosindex
```

### Clean Up

```bash
# Stop services (keep data)
docker compose down

# Stop services and remove volumes (DELETE ALL DATA)
docker compose down -v

# Remove unused images
docker system prune -a
```

## Networking

All services communicate over a dedicated bridge network (`photos-network`):

```
web (4200)
  └─> api (8080)
       ├─> postgres (5432)
       └─> aspire-dashboard (18889 OTLP)

indexing-service
  ├─> api (8080)
  └─> postgres (5432)
  └─> aspire-dashboard (18889 OTLP)

cleaner-service
  ├─> api (8080)
  └─> postgres (5432)
  └─> aspire-dashboard (18889 OTLP)
```

## Volume Management

### Named Volumes

- `postgres_data`: PostgreSQL database files
- `indexing_thumbnails`: Generated thumbnail images
- `cleaner_logs`: Deletion transaction logs

### Inspect Volumes

```bash
# List volumes
docker volume ls

# Inspect volume
docker volume inspect photos-index_postgres_data

# Backup volume
docker run --rm -v photos-index_postgres_data:/data -v $(pwd):/backup alpine tar czf /backup/postgres_backup.tar.gz /data
```

## Troubleshooting

### Service Won't Start

```bash
# Check service status
docker compose ps

# View service logs
docker compose logs <service-name>

# Restart service
docker compose restart <service-name>
```

### Database Connection Issues

1. Check PostgreSQL is healthy:
   ```bash
   docker compose ps postgres
   ```

2. Verify connection string in API logs:
   ```bash
   docker compose logs api | grep -i connection
   ```

3. Test database connection:
   ```bash
   docker compose exec postgres psql -U photosuser -d photosindex -c "SELECT 1;"
   ```

### Port Conflicts

If ports are already in use, edit `.env`:

```env
POSTGRES_PORT=5433
API_PORT=5001
WEB_PORT=4201
ASPIRE_DASHBOARD_PORT=18889
```

### Out of Memory

Add resource limits to docker-compose.yml:

```yaml
services:
  api:
    deploy:
      resources:
        limits:
          memory: 512M
```

### Indexing Performance

For large photo libraries:

1. Increase indexing interval:
   ```env
   INDEXING_INTERVAL_MINUTES=1440  # Daily
   ```

2. Monitor memory usage:
   ```bash
   docker stats
   ```

3. Check Aspire Dashboard for performance metrics:
   http://localhost:18888

## Security Best Practices

1. **Change Default Passwords:**
   - Set strong `POSTGRES_PASSWORD`

2. **Use Production Environment:**
   ```env
   ASPNETCORE_ENVIRONMENT=Production
   ```

3. **Enable HTTPS:**
   - Use reverse proxy (nginx, Traefik, Caddy)
   - Configure SSL certificates

4. **Network Isolation:**
   - Don't expose internal ports unnecessarily
   - Use Docker networks for service communication

5. **Regular Updates:**
   ```bash
   docker compose pull
   docker compose up -d
   ```

6. **Backup Regularly:**
   - Database backups
   - Configuration backups
   - Transaction logs (for cleaner service)

## Monitoring

### Aspire Dashboard

Access http://localhost:18888 to view:
- **Structured Logs:** All service logs in one place
- **Traces:** Request/response flows across services
- **Metrics:** Performance counters, memory usage

### Health Checks

```bash
# API health
curl http://localhost:5000/health

# Web health
curl http://localhost:4200/health

# PostgreSQL health
docker compose exec postgres pg_isready
```

### Resource Usage

```bash
# Real-time stats
docker stats

# Container inspection
docker compose exec api cat /proc/meminfo
```

## Upgrading

### Minor Updates (bug fixes)

```bash
docker compose down
docker compose pull
docker compose up -d
```

### Major Updates (schema changes)

```bash
# Backup database first
docker compose exec postgres pg_dump -U photosuser photosindex > backup.sql

# Stop services
docker compose down

# Update images
docker compose pull

# Start services (migrations run automatically)
docker compose up -d

# Verify
docker compose logs api | grep -i migration
```

## Development Tips

### Access Running Container

```bash
# Shell into API container
docker compose exec api /bin/bash

# Shell into PostgreSQL
docker compose exec postgres psql -U photosuser photosindex
```

### Run Database Migrations

```bash
# From host (requires .NET SDK)
cd ../../src
dotnet ef database update --project Database --startup-project Api

# From container
docker compose exec api dotnet ef database update --project Database
```

### Debugging

1. Set development environment:
   ```env
   ASPNETCORE_ENVIRONMENT=Development
   ```

2. Enable verbose logging:
   ```env
   Logging__LogLevel__Default=Debug
   ```

3. View real-time logs:
   ```bash
   docker compose logs -f --tail=100 api
   ```

## Additional Resources

- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard)
- [PostgreSQL Docker Hub](https://hub.docker.com/_/postgres)
- [nginx Docker Hub](https://hub.docker.com/_/nginx)

## Support

For issues or questions:
1. Check service logs: `docker compose logs <service>`
2. View Aspire Dashboard: http://localhost:18888
3. Verify configuration: `docker compose config`
4. Review documentation in `/docs` directory
