# Shared Traefik Configuration

This directory contains the dynamic routing configuration for Traefik, shared across deployment methods.

## Files

- `dynamic-compose.yml` - For Docker Compose deployments (uses Docker network DNS names)
- `dynamic-podman.yml` - For Podman/Kubernetes deployments (uses localhost since all containers run in the same pod)

## Routes Defined

| Path | Service | Description |
|------|---------|-------------|
| `/thumbnails/*` | MinIO | Direct thumbnail access (strips prefix) |
| `/api/swagger/*` | API | Swagger UI |
| `/hubs/*` | API | SignalR WebSocket connections |
| `/api/*` | API | REST API endpoints |
| `/` | Web | Angular SPA (catch-all) |

## Usage

### Docker Compose

The compose file mounts `dynamic-compose.yml` as `/etc/traefik/dynamic.yml`:

```yaml
volumes:
  - ../traefik/dynamic-compose.yml:/etc/traefik/dynamic.yml:ro
```

### Podman/Kubernetes

Copy `dynamic-podman.yml` to `deploy/kubernetes/traefik/dynamic.yml` (already done).

## Adding New Routes

When adding new routes:

1. Add to both `dynamic-compose.yml` and `dynamic-podman.yml`
2. Update the kubernetes copy: `cp dynamic-podman.yml ../kubernetes/traefik/dynamic.yml`
3. Update this README with the new route

## Service URLs

| Deployment | API | Web | MinIO |
|------------|-----|-----|-------|
| Docker Compose | `http://api:8080` | `http://web:80` | `http://minio:9000` |
| Podman | `http://localhost:5080` | `http://localhost:8090` | `http://localhost:9000` |
