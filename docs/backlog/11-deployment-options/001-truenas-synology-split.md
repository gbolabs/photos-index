# 001: TrueNAS + Synology Split Deployment

**Status**: 🔲 Not Started
**Priority**: P2
**Complexity**: Medium
**Estimated Effort**: 2-3 days

## Objective

Enable split deployment where:
- **Synology NAS**: Runs only the Indexing Service (lightweight, close to files)
- **TrueNAS SCALE**: Runs API, Database, Web UI, Aspire Dashboard, and Cleaner Service

This architecture leverages TrueNAS's superior resources (AMD CPU, 64GB RAM, SSD) for heavy processing while keeping file access local on Synology.

## Use Case

Users with:
- Synology NAS storing photos (often ARM-based, limited RAM)
- TrueNAS SCALE server with more powerful hardware
- Network connectivity between both systems

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      TrueNAS SCALE                               │
│                   (AMD CPU, 64GB RAM, SSD)                       │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    TrueNAS App (Helm)                     │   │
│  │                                                           │   │
│  │  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────────┐  │   │
│  │  │ Traefik │  │   API   │  │ Web UI  │  │   Aspire    │  │   │
│  │  │ :80/443 │  │  :5000  │  │  :80    │  │   :18888    │  │   │
│  │  └─────────┘  └─────────┘  └─────────┘  └─────────────┘  │   │
│  │       │            │                                      │   │
│  │       ▼            ▼                                      │   │
│  │  ┌─────────────────────────────────────┐                  │   │
│  │  │         PostgreSQL :5432            │                  │   │
│  │  │    (TrueNAS dataset for data)       │                  │   │
│  │  └─────────────────────────────────────┘                  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  Optional: Mount Synology photos via NFS for Cleaner Service    │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ HTTPS API calls
                              │ (POST /api/files/ingest)
                              │
┌─────────────────────────────────────────────────────────────────┐
│                        Synology NAS                              │
│                    (ARM/x86, limited RAM)                        │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │               Docker: Indexing Service                    │   │
│  │                                                           │   │
│  │  • Scans configured directories                           │   │
│  │  • Computes SHA256 hashes                                 │   │
│  │  • Extracts image metadata                                │   │
│  │  • Generates thumbnails                                   │   │
│  │  • POSTs results to TrueNAS API                          │   │
│  │                                                           │   │
│  │  Environment:                                             │   │
│  │    API_URL=http://truenas.local:5000                     │   │
│  │                                                           │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                     [Photo Directories]                          │
│                    /volume1/photos                               │
└─────────────────────────────────────────────────────────────────┘
```

## Implementation Tasks

### 1. TrueNAS Helm Chart
Create a Helm chart for TrueNAS SCALE's app catalog:

```
deploy/truenas/
├── Chart.yaml
├── values.yaml
├── templates/
│   ├── deployment-api.yaml
│   ├── deployment-web.yaml
│   ├── deployment-aspire.yaml
│   ├── service-api.yaml
│   ├── service-web.yaml
│   ├── ingress.yaml
│   ├── configmap.yaml
│   ├── secret.yaml
│   └── pvc-postgres.yaml
└── README.md
```

### 2. Synology Indexer-Only Compose
Create a minimal Docker Compose for Synology that only runs the indexer:

```yaml
# deploy/synology-indexer/docker-compose.yml
services:
  indexer:
    image: ghcr.io/gbolabs/photos-index-indexer:latest
    environment:
      - API_URL=http://truenas.local:5000
      - SCAN_DIRECTORIES=/photos
      - SCAN_INTERVAL_MINUTES=60
    volumes:
      - /volume1/photos:/photos:ro
    restart: unless-stopped
```

### 3. Network Configuration
- TrueNAS API must be accessible from Synology
- Options: Direct IP, DNS name, or Tailscale/WireGuard
- HTTPS recommended for cross-network communication

### 4. File Access for Cleaner Service
The Cleaner Service on TrueNAS needs file access to delete duplicates:
- **Option A**: Mount Synology share via NFS/SMB on TrueNAS
- **Option B**: Cleaner Service runs on Synology (separate container)
- **Option C**: API proxies delete requests to Synology agent

## Files to Create

| Path | Description |
|------|-------------|
| `deploy/truenas/Chart.yaml` | Helm chart metadata |
| `deploy/truenas/values.yaml` | Default configuration |
| `deploy/truenas/templates/*.yaml` | Kubernetes resources |
| `deploy/truenas/README.md` | TrueNAS installation guide |
| `deploy/synology-indexer/docker-compose.yml` | Indexer-only Synology deploy |
| `deploy/synology-indexer/README.md` | Synology indexer setup guide |
| `docs/deployment/truenas-synology-split.md` | Full deployment guide |

## Configuration

### TrueNAS values.yaml
```yaml
# API Service
api:
  image: ghcr.io/gbolabs/photos-index-api:latest
  replicas: 1
  resources:
    requests:
      memory: "256Mi"
      cpu: "100m"
    limits:
      memory: "1Gi"
      cpu: "1000m"

# PostgreSQL
postgresql:
  enabled: true
  persistence:
    enabled: true
    size: 10Gi
    storageClass: "local-path"  # TrueNAS dataset

# Web UI
web:
  image: ghcr.io/gbolabs/photos-index-web:latest
  replicas: 1

# Ingress
ingress:
  enabled: true
  hostname: photos.local

# Aspire Dashboard
aspire:
  enabled: true
  port: 18888
```

### Synology Environment Variables
```bash
# Required
API_URL=http://192.168.1.100:5000  # TrueNAS IP

# Optional
SCAN_DIRECTORIES=/photos
SCAN_INTERVAL_MINUTES=60
LOG_LEVEL=Information
OTEL_EXPORTER_OTLP_ENDPOINT=http://192.168.1.100:18889  # Send traces to TrueNAS
```

## Acceptance Criteria

- [ ] Helm chart installs successfully on TrueNAS SCALE
- [ ] Synology indexer connects to TrueNAS API
- [ ] Files indexed on Synology appear in TrueNAS UI
- [ ] Thumbnails generated and accessible
- [ ] Aspire Dashboard shows traces from both systems
- [ ] Documentation covers full setup process

## Security Considerations

1. **API Authentication**: Consider adding API key for indexer → API communication
2. **Network Isolation**: Use VLAN or VPN if systems are on different networks
3. **TLS**: Enable HTTPS for API endpoint in production
4. **Firewall**: Only expose required ports between systems

## Testing Plan

1. Deploy TrueNAS Helm chart in test environment
2. Deploy Synology indexer pointing to TrueNAS
3. Verify file indexing works across network
4. Test with sample photo directory
5. Verify Aspire receives telemetry from both services
6. Test Cleaner Service file access (if using NFS mount)

## Dependencies

- TrueNAS SCALE 24.04+ (for Helm chart support)
- Synology DSM 7.x with Container Manager
- Network connectivity between systems
- Container images published to registry

## Future Enhancements

1. **TrueNAS App Catalog**: Submit chart to TrueCharts or official catalog
2. **Auto-Discovery**: Indexer discovers API via mDNS
3. **Bidirectional Sync**: Support indexing files on both NAS systems
4. **HA Setup**: Multiple API replicas with load balancing
