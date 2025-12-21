# Photos Index - Synology Indexer Only

This deployment runs **only the indexing service** on your Synology NAS. All other services (API, Database, Web UI) run on TrueNAS SCALE.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                  TrueNAS SCALE                       │
│  ┌─────────┐  ┌─────────┐  ┌──────┐  ┌──────────┐  │
│  │ Traefik │──│   API   │  │ Web  │  │PostgreSQL│  │
│  │  :8050  │  │  :8080  │  │  :80 │  │  :5432   │  │
│  └────▲────┘  └─────────┘  └──────┘  └──────────┘  │
│       │                                             │
│  ┌────┴────┐                                        │
│  │ Aspire  │ OTLP: 8053                             │
│  │  :8052  │                                        │
│  └────▲────┘                                        │
└───────┼─────────────────────────────────────────────┘
        │
        │ HTTP API calls (:8050/api)
        │ OTLP telemetry (:8053)
        │
┌───────┴─────────────────────────────────────────────┐
│                   Synology NAS                       │
│  ┌───────────────────────────────────────────────┐  │
│  │              Indexing Service                 │  │
│  │  • Scans photo directories                    │  │
│  │  • Computes file hashes                       │  │
│  │  • Generates thumbnails                       │  │
│  │  • Sends data to TrueNAS API                  │  │
│  │  • Sends telemetry to Aspire                  │  │
│  └───────────────────────────────────────────────┘  │
│                       │                              │
│                 [Photo Files]                        │
│                /volume1/photos                       │
└─────────────────────────────────────────────────────┘
```

## Prerequisites

1. **TrueNAS SCALE** with Photos Index deployed (see `deploy/truenas/`)
2. **Synology DSM 7.x** with Container Manager (Docker)
3. **Network connectivity** between Synology and TrueNAS

## Installation

### 1. Prepare the Configuration

```bash
# On Synology, create a directory for the deployment
mkdir -p /volume1/docker/photos-indexer
cd /volume1/docker/photos-indexer

# Download the files (or copy from this repo)
wget https://raw.githubusercontent.com/gbolabs/photos-index/main/deploy/synology-indexer/docker-compose.yml
wget https://raw.githubusercontent.com/gbolabs/photos-index/main/deploy/synology-indexer/.env.example

# Create your configuration
cp .env.example .env
```

### 2. Configure Environment Variables

Edit `.env` and set:

```bash
# Your TrueNAS IP - use the Traefik port with /api path
# IMPORTANT: Use API_BASE_URL, not API_URL
API_BASE_URL=http://192.168.1.100:8050/api

# Path to your photos on Synology
PHOTOS_PATH=/volume1/photos

# Optional: Scan interval (default: 60 minutes)
SCAN_INTERVAL_MINUTES=60

# Optional: Send telemetry to Aspire on TrueNAS
OTEL_ENDPOINT=http://192.168.1.100:8053
```

### 3. Start the Indexer

Using Container Manager UI:
1. Open Container Manager → Project
2. Click "Create" → "Create from docker-compose.yml"
3. Select the `docker-compose.yml` file
4. Start the project

Or via SSH:
```bash
cd /volume1/docker/photos-indexer
docker compose up -d
```

### 4. Verify Operation

```bash
# Check logs
docker compose logs -f indexer

# You should see:
# - Connection to API successful
# - Starting scan of /photos
# - Found X files
# - Indexed X files successfully
```

Check Aspire dashboard on TrueNAS (`http://truenas:8052`) for traces from `photos-index-indexer-synology`.

## Configuration Options

### Multiple Photo Directories

Edit `docker-compose.yml` to add more volumes:

```yaml
volumes:
  - /volume1/photos:/photos:ro
  - /volume1/backup/photos:/backup:ro
  - /volume2/camera:/camera:ro

environment:
  - SCAN_DIRECTORIES=/photos,/backup,/camera
```

### Resource Limits

**Note**: Synology's Docker implementation doesn't support the `deploy` section with resource limits. The container uses `DOTNET_GCHeapHardLimit` environment variable to limit memory usage instead:

```yaml
environment:
  # Limit .NET memory to ~200MB (in bytes)
  - DOTNET_GCHeapHardLimit=200000000
```

### OpenTelemetry Tracing

Send traces to TrueNAS Aspire Dashboard:

```bash
# In .env
OTEL_ENDPOINT=http://truenas-ip:8053
```

## Troubleshooting

### Cannot connect to API

1. Verify TrueNAS IP is correct: `ping truenas-ip`
2. Check API is running: `curl http://truenas-ip:8050/api/files/stats`
3. Check firewall allows port 8050

### Indexer not finding files

1. Verify volume mount: `docker exec photos-indexer ls /photos`
2. Check directory permissions
3. Ensure photos have supported extensions (.jpg, .png, etc.)

### Permission denied errors

The container runs as root (`user: "0:0"`) to ensure it can read all photo files regardless of Synology's complex permission model. This is safe because:
- All volumes are mounted read-only (`:ro`)
- The container cannot modify your photos

### High CPU/Memory usage

1. Lower scan interval (`SCAN_INTERVAL_MINUTES`)
2. Adjust `DOTNET_GCHeapHardLimit` for memory control
3. Use Container Manager UI to set container resource limits

## Updating

```bash
cd /volume1/docker/photos-indexer
docker compose pull
docker compose up -d
```

## Logs

```bash
# View logs
docker compose logs -f

# View last 100 lines
docker compose logs --tail 100
```

## Stopping

```bash
docker compose down
```
