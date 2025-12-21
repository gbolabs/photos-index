# Photos Index - TrueNAS SCALE Deployment

Deploy Photos Index on TrueNAS SCALE using the Custom App feature.

## Quick Start (Docker Compose)

TrueNAS SCALE supports Docker Compose directly via **Apps > Discover Apps > Custom App > Install via YAML**.

### 1. Prepare Storage

Create datasets for persistent data:

```bash
# Via TrueNAS UI: Datasets > Add Dataset
# Create two datasets:
# - One for PostgreSQL data (e.g., hdr1/dbs/photos-index)
# - One for thumbnails (e.g., hdr1/apps/photos-index)
```

### 2. Set Permissions

**Important**: PostgreSQL Alpine container runs as uid 70, API runs as uid 1000.

```bash
# PostgreSQL data directory - MUST be uid 70
sudo chown 70:70 /mnt/<pool>/dbs/photos-index
sudo chmod 700 /mnt/<pool>/dbs/photos-index

# Thumbnails directory - uid 1000
sudo chown 1000:1000 /mnt/<pool>/apps/photos-index
```

### 3. Deploy the App

1. Go to **Apps > Discover Apps > Custom App**
2. Click **Install via YAML**
3. Paste the contents of `docker-compose.yml`
4. **Update the volume paths** to match your datasets
5. Set a secure database password (replace `changeme`)
6. Click **Save**

### 4. Access the Application

| Service | URL | Purpose |
|---------|-----|---------|
| Web UI | `http://truenas:8050` | Main application |
| API | `http://truenas:8050/api/` | REST API (via Traefik) |
| Aspire | `http://truenas:8052` | Observability dashboard |
| Traefik | `http://truenas:8054` | Reverse proxy dashboard |
| OTLP | `http://truenas:8053` | Telemetry receiver (for indexer) |

## Working Configuration

```yaml
name: photos-index

services:
  traefik:
    image: traefik:v3.2
    container_name: photos-index-traefik
    restart: unless-stopped
    command:
      - "--api.insecure=true"
      - "--providers.docker=true"
      - "--providers.docker.exposedbydefault=false"
      - "--entrypoints.web.address=:80"
    ports:
      - "8050:80"
      - "8054:8080"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro

  postgres:
    image: postgres:16-alpine
    container_name: photos-index-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: photosindex
      POSTGRES_USER: photosindex
      POSTGRES_PASSWORD: changeme
    volumes:
      # UPDATE THIS PATH to your PostgreSQL dataset
      - /mnt/hdr1/dbs/photos-index:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U photosindex -d photosindex"]
      interval: 10s
      timeout: 5s
      retries: 5

  api:
    image: ghcr.io/gbolabs/photos-index/api:0.0.1
    container_name: photos-index-api
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ASPNETCORE_URLS: "http://+:8080"
      ASPNETCORE_ENVIRONMENT: "Development"
      ASPNETCORE_HTTPS_PORTS: ""
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=photosindex;Username=photosindex;Password=changeme"
      ThumbnailDirectory: /data/thumbnails
      OTEL_EXPORTER_OTLP_ENDPOINT: "http://aspire:18889"
      OTEL_SERVICE_NAME: photos-index-api
    volumes:
      # UPDATE THIS PATH to your thumbnails dataset
      - /mnt/hdr1/apps/photos-index:/data/thumbnails
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.api.rule=PathPrefix(`/api`)"
      - "traefik.http.routers.api.entrypoints=web"
      - "traefik.http.routers.api.priority=100"
      - "traefik.http.services.api.loadbalancer.server.port=8080"

  web:
    image: ghcr.io/gbolabs/photos-index/web:0.0.1
    container_name: photos-index-web
    restart: unless-stopped
    depends_on:
      - api
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.web.rule=PathPrefix(`/`)"
      - "traefik.http.routers.web.entrypoints=web"
      - "traefik.http.routers.web.priority=1"
      - "traefik.http.services.web.loadbalancer.server.port=80"

  aspire:
    image: mcr.microsoft.com/dotnet/aspire-dashboard:9.1
    container_name: photos-index-aspire
    restart: unless-stopped
    environment:
      DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS: "true"
    ports:
      - "8052:18888"
      - "8053:18889"
```

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      TrueNAS SCALE                               │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                Custom App (Docker Compose)                │   │
│  │                                                           │   │
│  │  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────────┐  │   │
│  │  │ Traefik │──│   API   │  │ Web UI  │  │   Aspire    │  │   │
│  │  │  :8050  │  │  :8080  │  │   :80   │  │ :8052/:8053 │  │   │
│  │  └─────────┘  └────┬────┘  └─────────┘  └─────────────┘  │   │
│  │       │            │                           ▲          │   │
│  │       └────────────┼───────────────────────────┘          │   │
│  │                    │                      OTLP            │   │
│  │               ┌────▼────┐                                 │   │
│  │               │PostgreSQL│                                │   │
│  │               │  :5432  │                                 │   │
│  │               └────┬────┘                                 │   │
│  │                    │                                      │   │
│  └────────────────────┼──────────────────────────────────────┘   │
│                       │                                          │
│              ┌────────▼────────┐                                 │
│              │  ZFS Datasets   │                                 │
│              │   /mnt/hdr1/    │                                 │
│              └─────────────────┘                                 │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ HTTP API (:8050/api)
                              │ OTLP (:8053)
┌─────────────────────────────────────────────────────────────────┐
│                        Synology NAS                              │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │               Indexing Service (Docker)                   │   │
│  │  API_URL=http://truenas:8050/api                         │   │
│  │  OTEL_ENDPOINT=http://truenas:8053                       │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                     [Photo Directories]                          │
└─────────────────────────────────────────────────────────────────┘
```

## Important Notes

### Port 80 is Usually in Use

TrueNAS SCALE typically uses port 80 for its own services. The configuration uses port **8050** as the main entry point instead.

### Aspire Dashboard Must Be Direct

The Aspire dashboard doesn't work behind a reverse proxy with path prefix stripping. It's exposed directly on port **8052**.

### Image Names

The correct image format is:
- `ghcr.io/gbolabs/photos-index/api:<version>`
- `ghcr.io/gbolabs/photos-index/web:<version>`
- `ghcr.io/gbolabs/photos-index/indexing-service:<version>`

**NOT** `ghcr.io/gbolabs/photos-index-api` (wrong format).

### PostgreSQL Permissions

The `postgres:16-alpine` image runs as **uid 70**, not 999. Always set:
```bash
sudo chown 70:70 /mnt/<pool>/dbs/photos-index
```

### HTTPS Redirect

The API has HTTPS redirect middleware. Disable it with:
```yaml
environment:
  ASPNETCORE_ENVIRONMENT: "Development"
  ASPNETCORE_HTTPS_PORTS: ""
```

## Connecting Synology Indexer

Once TrueNAS is running, configure the Synology indexer:

```bash
# On Synology - see deploy/synology-indexer/
API_URL=http://truenas-ip:8050/api
OTEL_EXPORTER_OTLP_ENDPOINT=http://truenas-ip:8053
```

The indexer will:
1. Scan photos on Synology
2. Compute hashes and generate thumbnails
3. POST metadata to TrueNAS API
4. Send telemetry to Aspire dashboard

## Updating

Update to a new version:

```bash
# Edit the YAML and change image tags
image: ghcr.io/gbolabs/photos-index/api:0.0.2
```

Or use the IMAGE_VERSION variable:

```yaml
image: ghcr.io/gbolabs/photos-index/api:${IMAGE_VERSION:-latest}
```

## Troubleshooting

### Container Logs

```bash
# Via TrueNAS shell
docker logs photos-index-api
docker logs photos-index-postgres
docker logs photos-index-web
```

### PostgreSQL Unhealthy

```bash
# Check permissions
sudo ls -la /mnt/<pool>/dbs/photos-index/

# Should show uid 70
# If not, fix permissions:
sudo rm -rf /mnt/<pool>/dbs/photos-index/*
sudo chown 70:70 /mnt/<pool>/dbs/photos-index
```

### Port Already in Use

If you see "bind: address already in use", another service is using that port. Change to a different port in the YAML (e.g., 8050 instead of 80).

### Traefik Still in Error Logs

If you removed Traefik but still see it in error logs, TrueNAS may have cached the old YAML. Delete the app completely and reinstall.

### API Returns 404

Check the exact path. API endpoints are at `/api/files/stats`, `/api/duplicates`, etc. The `/health` endpoint may not exist in all versions.
