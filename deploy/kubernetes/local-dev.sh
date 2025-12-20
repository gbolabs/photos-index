#!/bin/bash
# Local development script for Podman
# Uses podman kube play with Kubernetes Pod manifests

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
MANIFEST="$SCRIPT_DIR/photos-index.yaml"

# Default photos path for local development
PHOTOS_PATH="${PHOTOS_PATH:-$HOME/Pictures}"

usage() {
    echo "Usage: $0 {build|start|stop|logs|status|psql}"
    echo ""
    echo "Commands:"
    echo "  build   - Build all container images"
    echo "  start   - Start all services with podman kube play"
    echo "  stop    - Stop all services"
    echo "  logs    - Show logs for all containers"
    echo "  status  - Show status of pods and containers"
    echo "  psql    - Open psql shell to PostgreSQL database"
    echo ""
    echo "Environment variables:"
    echo "  PHOTOS_PATH - Path to photos directory (default: ~/Pictures)"
}

build_images() {
    echo "Building container images..."

    podman build -t localhost/photos-index-api:latest \
        -f "$PROJECT_ROOT/deploy/docker/api/Dockerfile" \
        "$PROJECT_ROOT"

    podman build -t localhost/photos-index-indexing-service:latest \
        -f "$PROJECT_ROOT/deploy/docker/indexing-service/Dockerfile" \
        "$PROJECT_ROOT"

    podman build -t localhost/photos-index-cleaner-service:latest \
        -f "$PROJECT_ROOT/deploy/docker/cleaner-service/Dockerfile" \
        "$PROJECT_ROOT"

    podman build -t localhost/photos-index-web:latest \
        -f "$PROJECT_ROOT/deploy/docker/web/Dockerfile" \
        "$PROJECT_ROOT"

    echo ""
    echo "All images built successfully"
    podman images | grep photos-index
}

start_services() {
    echo "Starting services..."

    # Create photos directory if it doesn't exist
    mkdir -p "$PHOTOS_PATH"

    # Generate manifest with photos path substituted
    sed "s|path: /tmp/photos|path: $PHOTOS_PATH|g" "$MANIFEST" | podman kube play -

    echo ""
    echo "Services starting. Access:"
    echo "  Application:      http://localhost:8080 (via Traefik)"
    echo "  API:              http://localhost:8080/api (via Traefik)"
    echo "  Traefik Dashboard: http://localhost:8081"
    echo "  Aspire Dashboard: http://localhost:18888"
    echo "  PostgreSQL:       localhost:5432"
    echo ""
    echo "Photos directory: $PHOTOS_PATH"
    echo ""
    echo "Run '$0 status' to check container status"
    echo "Run '$0 logs' to view logs"
}

stop_services() {
    echo "Stopping services..."
    podman kube play --down "$MANIFEST"
    echo "Services stopped"
}

show_logs() {
    # Show logs for all containers in the pod
    podman pod logs -f photos-index
}

show_status() {
    echo "=== Pods ==="
    podman pod ps
    echo ""
    echo "=== Containers ==="
    podman ps -a --pod --filter "pod=photos-index"
}

run_psql() {
    echo "Connecting to PostgreSQL..."
    podman exec -it photos-index-postgres psql -U photosuser -d photosindex
}

case "${1:-}" in
    build)
        build_images
        ;;
    start)
        start_services
        ;;
    stop)
        stop_services
        ;;
    logs)
        show_logs
        ;;
    status)
        show_status
        ;;
    psql)
        run_psql
        ;;
    *)
        usage
        exit 1
        ;;
esac
