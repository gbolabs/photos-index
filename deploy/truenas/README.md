# Photos Index - TrueNAS SCALE Deployment

Deploy Photos Index on TrueNAS SCALE using the Custom App feature.

## Quick Start (Docker Compose)

TrueNAS SCALE supports Docker Compose directly via **Apps > Discover Apps > Custom App > Install via YAML**.

### 1. Prepare Storage

Create datasets for persistent data:

```bash
# Via TrueNAS UI: Datasets > Add Dataset
# Or via CLI:
zfs create tank/apps/photos-index
zfs create tank/apps/photos-index/postgres
zfs create tank/apps/photos-index/thumbnails
```

### 2. Deploy the App

1. Go to **Apps > Discover Apps > Custom App**
2. Click **Install via YAML**
3. Paste the contents of `docker-compose.yml`
4. **Important**: Update the volume paths to match your datasets:
   ```yaml
   volumes:
     - /mnt/tank/apps/photos-index/postgres:/var/lib/postgresql/data
     - /mnt/tank/apps/photos-index/thumbnails:/data/thumbnails
   ```
5. Set a secure database password (replace `changeme`)
6. Click **Save**

### 3. Access the Application

| Service | URL | Purpose |
|---------|-----|---------|
| Web UI | `http://truenas:8080` | Main application |
| API | `http://truenas:5000` | REST API (for Synology indexer) |
| Aspire | `http://truenas:18888` | Observability dashboard |
| Traefik | `http://truenas:8081` | Reverse proxy dashboard |

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      TrueNAS SCALE                               │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                Custom App (Docker Compose)                │   │
│  │                                                           │   │
│  │  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────────┐  │   │
│  │  │ Traefik │  │   API   │  │ Web UI  │  │   Aspire    │  │   │
│  │  │  :80    │  │  :5000  │  │  :8080  │  │   :18888    │  │   │
│  │  └─────────┘  └────┬────┘  └─────────┘  └─────────────┘  │   │
│  │                    │                                      │   │
│  │               ┌────▼────┐                                 │   │
│  │               │PostgreSQL│                                │   │
│  │               │  :5432  │                                 │   │
│  │               └────┬────┘                                 │   │
│  │                    │                                      │   │
│  └────────────────────┼──────────────────────────────────────┘   │
│                       │                                          │
│              ┌────────▼────────┐                                 │
│              │  ZFS Datasets   │                                 │
│              │ /mnt/tank/apps/ │                                 │
│              └─────────────────┘                                 │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ HTTP API calls
                              │
┌─────────────────────────────────────────────────────────────────┐
│                        Synology NAS                              │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │               Indexing Service (Docker)                   │   │
│  │  API_URL=http://truenas:5000                             │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                     [Photo Directories]                          │
└─────────────────────────────────────────────────────────────────┘
```

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `POSTGRES_PASSWORD` | `changeme` | Database password (change this!) |

### Volume Mounts

TrueNAS recommends **Host Path** volumes pointing to ZFS datasets:

| Container Path | Purpose | Recommended Dataset |
|----------------|---------|---------------------|
| `/var/lib/postgresql/data` | Database files | `tank/apps/photos-index/postgres` |
| `/data/thumbnails` | Generated thumbnails | `tank/apps/photos-index/thumbnails` |

### Port Mapping

| Host Port | Container | Service |
|-----------|-----------|---------|
| 80 | traefik:80 | Reverse proxy (optional) |
| 5000 | api:5000 | REST API |
| 8080 | web:80 | Web UI |
| 8081 | traefik:8080 | Traefik dashboard |
| 18888 | aspire:18888 | Aspire dashboard |
| 18889 | aspire:18889 | OTLP receiver |

## Connecting Synology Indexer

Once TrueNAS is running, configure the Synology indexer:

```bash
# On Synology - see deploy/synology-indexer/
API_URL=http://truenas-ip:5000
```

The indexer will:
1. Scan photos on Synology
2. Compute hashes and generate thumbnails
3. POST metadata to TrueNAS API
4. TrueNAS stores data in PostgreSQL

## Alternative: Helm Chart

For advanced Kubernetes deployments, a Helm chart is also available in `templates/`. This is useful if you prefer Helm or need more customization.

```bash
helm install photos-index ./deploy/truenas -n photos-index
```

## Updating

1. Go to **Apps > Installed Apps > photos-index**
2. Click the three-dot menu > **Edit**
3. Update image tags to latest versions
4. Click **Save**

Or pull new images:
```bash
docker compose pull
docker compose up -d
```

## Troubleshooting

### Check Container Logs

```bash
# Via TrueNAS UI: Apps > photos-index > Logs
# Or via CLI:
docker logs photos-index-api
docker logs photos-index-postgres
```

### Database Connection Issues

```bash
# Check PostgreSQL is healthy
docker exec photos-index-postgres pg_isready -U photosindex

# Check API can reach database
docker logs photos-index-api | grep -i database
```

### Permission Issues

Ensure datasets have correct permissions:
```bash
# TrueNAS datasets typically use uid/gid 568
chown -R 568:568 /mnt/tank/apps/photos-index/
```

### Synology Can't Connect

1. Verify TrueNAS IP is reachable from Synology
2. Check port 5000 is not blocked by firewall
3. Test: `curl http://truenas-ip:5000/health`
