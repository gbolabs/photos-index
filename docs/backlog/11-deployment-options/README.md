# 11: Deployment Options

Alternative deployment architectures for different infrastructure scenarios.

## Overview

The default deployment runs all services on a single machine (Synology NAS via Docker Compose or Podman). This section covers alternative architectures for users with multiple systems.

## Available Options

| Option | Description | Use Case |
|--------|-------------|----------|
| `001-truenas-synology-split.md` | Split deployment: Indexer on Synology, everything else on TrueNAS | Users with both NAS types |
| (Future) `002-kubernetes-cluster.md` | Full Kubernetes deployment | Production/enterprise |
| (Future) `003-cloud-hybrid.md` | Cloud DB + local services | Cloud-native users |

## Architecture Comparison

### Default: Single Node (Synology)
```
┌─────────────────────────────────────────────────┐
│                 Synology NAS                     │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌────────┐ │
│  │ Traefik │ │   API   │ │ Indexer │ │ Web UI │ │
│  └─────────┘ └─────────┘ └─────────┘ └────────┘ │
│  ┌─────────┐ ┌─────────┐                        │
│  │Postgres │ │ Aspire  │                        │
│  └─────────┘ └─────────┘                        │
│                    │                             │
│              [Photo Files]                       │
└─────────────────────────────────────────────────┘
```

### Option 001: TrueNAS + Synology Split
```
┌─────────────────────────────────────────────────┐
│                  TrueNAS SCALE                   │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌────────┐ │
│  │ Traefik │ │   API   │ │Postgres │ │ Web UI │ │
│  └─────────┘ └─────────┘ └─────────┘ └────────┘ │
│  ┌─────────┐ ┌─────────┐                        │
│  │ Aspire  │ │ Cleaner │                        │
│  └─────────┘ └─────────┘                        │
└─────────────────────────────────────────────────┘
         ▲                         │
         │ API calls               │ File access (NFS/SMB)
         │                         ▼
┌─────────────────────────────────────────────────┐
│                 Synology NAS                     │
│  ┌─────────────────────────────────────────────┐│
│  │              Indexing Service                ││
│  │  • File scanning                             ││
│  │  • Hash computation                          ││
│  │  • Thumbnail generation                      ││
│  │  • Sends results to TrueNAS API             ││
│  └─────────────────────────────────────────────┘│
│                    │                             │
│              [Photo Files]                       │
└─────────────────────────────────────────────────┘
```

## Benefits of Split Deployment

1. **Resource Optimization**: Heavy services (DB, API) run on powerful hardware
2. **Minimal Synology Load**: Only lightweight indexer runs on NAS
3. **Better Performance**: PostgreSQL benefits from SSD and more RAM
4. **Scalability**: TrueNAS can be upgraded independently
5. **Photo Locality**: Files stay on Synology, only metadata transmitted
