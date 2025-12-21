# Photos Index - Synology Indexer Only

This deployment runs **only the indexing service** on your Synology NAS. All other services (API, Database, Web UI) run on TrueNAS SCALE.

## Architecture

```
┌─────────────────────────────────────────────┐
│              TrueNAS SCALE                   │
│  ┌─────────┐  ┌─────────┐  ┌─────────────┐  │
│  │   API   │  │ Web UI  │  │  PostgreSQL │  │
│  │  :5000  │  │  :80    │  │    :5432    │  │
│  └────▲────┘  └─────────┘  └─────────────┘  │
└───────┼─────────────────────────────────────┘
        │
        │ HTTP API calls
        │ (file metadata, thumbnails)
        │
┌───────┴─────────────────────────────────────┐
│              Synology NAS                    │
│  ┌─────────────────────────────────────┐    │
│  │         Indexing Service            │    │
│  │  • Scans photo directories          │    │
│  │  • Computes file hashes             │    │
│  │  • Generates thumbnails             │    │
│  │  • Sends data to TrueNAS API        │    │
│  └─────────────────────────────────────┘    │
│                    │                         │
│              [Photo Files]                   │
│             /volume1/photos                  │
└─────────────────────────────────────────────┘
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
# Your TrueNAS IP or hostname
API_URL=http://192.168.1.100:5000

# Path to your photos
PHOTOS_PATH=/volume1/photos

# Optional: Scan interval (default: 60 minutes)
SCAN_INTERVAL_MINUTES=60
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

Adjust for your Synology's capabilities:

```yaml
deploy:
  resources:
    limits:
      memory: 512M  # Increase if you have RAM to spare
      cpus: '1.0'   # Limit CPU usage
```

### OpenTelemetry Tracing

Send traces to TrueNAS Aspire Dashboard:

```bash
# In .env
OTEL_ENDPOINT=http://truenas.local:18889
```

## Troubleshooting

### Cannot connect to API

1. Verify TrueNAS IP is correct: `ping truenas.local`
2. Check API is running: `curl http://truenas.local:5000/health`
3. Check firewall allows port 5000

### Indexer not finding files

1. Verify volume mount: `docker exec photos-indexer ls /photos`
2. Check directory permissions
3. Ensure photos have supported extensions (.jpg, .png, etc.)

### High CPU/Memory usage

1. Reduce concurrent processing in environment
2. Lower scan interval
3. Add CPU/memory limits in docker-compose.yml

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
