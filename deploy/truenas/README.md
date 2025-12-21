# Photos Index - TrueNAS SCALE Deployment

Helm chart for deploying Photos Index on TrueNAS SCALE. Designed for split deployment where the indexer runs on Synology and everything else runs here.

## Architecture

This chart deploys:
- **API Service**: REST API for file management
- **Web UI**: Angular frontend served by nginx
- **PostgreSQL**: Database for indexed files and metadata
- **Aspire Dashboard**: OpenTelemetry collector and visualization

The **Indexing Service** runs separately on Synology (see `deploy/synology-indexer/`).

## Prerequisites

- TrueNAS SCALE 24.04 or later
- Helm 3.x
- kubectl configured for TrueNAS

## Quick Start

### 1. Add the Helm Repository (Future)

```bash
# Once published to a chart repository
helm repo add photos-index https://charts.example.com
helm repo update
```

### 2. Install from Local Chart

```bash
# Clone the repository
git clone https://github.com/gbolabs/photos-index.git
cd photos-index/deploy/truenas

# Create namespace
kubectl create namespace photos-index

# Install with default values
helm install photos-index . -n photos-index \
  --set postgresql.auth.password=your-secure-password

# Or with custom values
helm install photos-index . -n photos-index -f my-values.yaml
```

### 3. Configure Ingress

Edit `values.yaml` or use `--set`:

```bash
helm install photos-index . -n photos-index \
  --set ingress.hostname=photos.mynas.local \
  --set postgresql.auth.password=secure-password
```

## Configuration

### Key Values

| Parameter | Description | Default |
|-----------|-------------|---------|
| `api.replicas` | API pod replicas | `1` |
| `api.resources.limits.memory` | API memory limit | `1Gi` |
| `postgresql.auth.password` | Database password | Required |
| `postgresql.primary.persistence.size` | Database storage | `10Gi` |
| `ingress.hostname` | Ingress hostname | `photos.local` |
| `ingress.tls.enabled` | Enable HTTPS | `false` |

### Full Configuration

See `values.yaml` for all options.

### Example Custom Values

```yaml
# my-values.yaml

# Increase API resources
api:
  replicas: 2
  resources:
    limits:
      memory: 2Gi
      cpu: 2000m

# Enable TLS
ingress:
  hostname: photos.mydomain.com
  tls:
    enabled: true
    secretName: photos-tls

# Larger database
postgresql:
  primary:
    persistence:
      size: 50Gi
```

## Accessing Services

After installation:

| Service | URL |
|---------|-----|
| Web UI | `http://photos.local/` |
| API | `http://photos.local/api/` |
| Health | `http://photos.local/health` |
| Aspire | `http://aspire.photos.local/` |

## Connecting Synology Indexer

Once TrueNAS is running, configure the Synology indexer:

```bash
# On Synology
cd /volume1/docker/photos-indexer
echo "API_URL=http://truenas-ip:5000" > .env
echo "PHOTOS_PATH=/volume1/photos" >> .env
docker compose up -d
```

## Upgrading

```bash
helm upgrade photos-index . -n photos-index
```

## Uninstalling

```bash
helm uninstall photos-index -n photos-index

# Optional: Remove PVCs
kubectl delete pvc -n photos-index -l app.kubernetes.io/instance=photos-index
```

## TrueNAS App Catalog (Future)

This chart will be submitted to TrueCharts for easy installation via TrueNAS UI.

## Troubleshooting

### Check Pod Status

```bash
kubectl get pods -n photos-index
kubectl describe pod photos-index-api-xxx -n photos-index
```

### View Logs

```bash
kubectl logs -n photos-index -l app.kubernetes.io/component=api -f
```

### Database Connection Issues

```bash
# Check PostgreSQL is running
kubectl get pods -n photos-index -l app.kubernetes.io/component=postgresql

# Check connection from API pod
kubectl exec -n photos-index deploy/photos-index-api -- \
  curl -v telnet://photos-index-postgresql:5432
```

### Ingress Issues

```bash
# Check ingress configuration
kubectl get ingress -n photos-index
kubectl describe ingress photos-index-ingress -n photos-index
```
