#!/bin/bash
# Local development script for Podman

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Default photos path for local development
export PHOTOS_PATH="${PHOTOS_PATH:-$HOME/Pictures}"

usage() {
    echo "Usage: $0 {build|start|stop|logs|status}"
    echo ""
    echo "Commands:"
    echo "  build   - Build all container images"
    echo "  start   - Start all services with podman kube play"
    echo "  stop    - Stop all services"
    echo "  logs    - Show logs for all pods"
    echo "  status  - Show status of all pods"
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

    echo "All images built successfully"
}

start_services() {
    echo "Starting services..."

    # Generate manifests with kustomize and apply with podman
    kubectl kustomize "$SCRIPT_DIR" | podman kube play --replace -

    echo ""
    echo "Services starting. Access:"
    echo "  Web UI:           http://localhost:8080"
    echo "  API:              http://localhost:5000"
    echo "  Aspire Dashboard: http://localhost:18888"
    echo ""
    echo "Run '$0 status' to check pod status"
}

stop_services() {
    echo "Stopping services..."
    kubectl kustomize "$SCRIPT_DIR" | podman kube down -
    echo "Services stopped"
}

show_logs() {
    podman pod logs -f photos-index
}

show_status() {
    echo "Pods:"
    podman pod ps --filter name=photos-index
    echo ""
    echo "Containers:"
    podman ps --filter pod=photos-index
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
    *)
        usage
        exit 1
        ;;
esac
