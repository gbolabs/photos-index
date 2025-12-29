#!/bin/bash
# Local development script for Podman
# Uses podman kube play with Kubernetes Pod manifests

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
MANIFEST="$SCRIPT_DIR/photos-index.yaml"

# Default paths for local development
PHOTOS_PATH="${PHOTOS_PATH:-$HOME/Pictures}"
HOME_PICTURES_PATH="${HOME_PICTURES_PATH:-$HOME/Pictures}"

usage() {
    echo "Usage: $0 {build|pull|start|stop|restart|clean|logs|status|psql}"
    echo ""
    echo "Commands:"
    echo "  build   - Build all container images locally"
    echo "  pull    - Pull pre-built images from GHCR (faster, requires less memory)"
    echo "  start   - Start all services with podman kube play"
    echo "  stop    - Stop all services (preserves data)"
    echo "  restart - Stop and start services (preserves data)"
    echo "  clean   - Stop and remove all data (volumes, PVCs)"
    echo "  logs    - Show logs for all containers"
    echo "  status  - Show status of pods and containers"
    echo "  psql    - Open psql shell to PostgreSQL database"
    echo ""
    echo "Environment variables:"
    echo "  PHOTOS_PATH        - Path to photos directory (default: ~/Pictures)"
    echo "  HOME_PICTURES_PATH - Path to home pictures (default: ~/Pictures)"
    echo "  IMAGE_TAG          - Tag to pull from GHCR (default: main)"
}

build_images() {
    echo "Building container images..."

    # Get build info from git
    BUILD_COMMIT_HASH=$(git -C "$PROJECT_ROOT" rev-parse --short HEAD 2>/dev/null || echo "dev")
    BUILD_BRANCH=$(git -C "$PROJECT_ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null || echo "local")
    BUILD_TIME=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

    echo "Build info: commit=$BUILD_COMMIT_HASH, branch=$BUILD_BRANCH, time=$BUILD_TIME"
    echo ""

    # Use --no-cache to ensure fresh builds without reusing cached layers
    podman build --no-cache -t localhost/photos-index-api:latest \
        --build-arg BUILD_COMMIT_HASH="$BUILD_COMMIT_HASH" \
        --build-arg BUILD_BRANCH="$BUILD_BRANCH" \
        --build-arg BUILD_TIME="$BUILD_TIME" \
        -f "$PROJECT_ROOT/deploy/docker/api/Dockerfile" \
        "$PROJECT_ROOT"

    podman build --no-cache -t localhost/photos-index-indexing-service:latest \
        --build-arg BUILD_COMMIT_HASH="$BUILD_COMMIT_HASH" \
        --build-arg BUILD_BRANCH="$BUILD_BRANCH" \
        --build-arg BUILD_TIME="$BUILD_TIME" \
        -f "$PROJECT_ROOT/deploy/docker/indexing-service/Dockerfile" \
        "$PROJECT_ROOT"

    podman build --no-cache -t localhost/photos-index-cleaner-service:latest \
        --build-arg BUILD_COMMIT_HASH="$BUILD_COMMIT_HASH" \
        --build-arg BUILD_BRANCH="$BUILD_BRANCH" \
        --build-arg BUILD_TIME="$BUILD_TIME" \
        -f "$PROJECT_ROOT/deploy/docker/cleaner-service/Dockerfile" \
        "$PROJECT_ROOT"

    podman build --no-cache -t localhost/photos-index-metadata-service:latest \
        --build-arg BUILD_COMMIT_HASH="$BUILD_COMMIT_HASH" \
        --build-arg BUILD_BRANCH="$BUILD_BRANCH" \
        --build-arg BUILD_TIME="$BUILD_TIME" \
        -f "$PROJECT_ROOT/deploy/docker/metadata-service/Dockerfile" \
        "$PROJECT_ROOT"

    podman build --no-cache -t localhost/photos-index-thumbnail-service:latest \
        --build-arg BUILD_COMMIT_HASH="$BUILD_COMMIT_HASH" \
        --build-arg BUILD_BRANCH="$BUILD_BRANCH" \
        --build-arg BUILD_TIME="$BUILD_TIME" \
        -f "$PROJECT_ROOT/deploy/docker/thumbnail-service/Dockerfile" \
        "$PROJECT_ROOT"

    podman build --no-cache -t localhost/photos-index-web:latest \
        --build-arg BUILD_COMMIT_HASH="$BUILD_COMMIT_HASH" \
        --build-arg BUILD_BRANCH="$BUILD_BRANCH" \
        --build-arg BUILD_TIME="$BUILD_TIME" \
        -f "$PROJECT_ROOT/deploy/docker/web/Dockerfile" \
        "$PROJECT_ROOT"

    echo ""
    echo "All images built successfully"
    podman images | grep photos-index
}

pull_images() {
    # Allow overriding the image tag (useful for testing specific versions)
    IMAGE_TAG="${IMAGE_TAG:-main}"
    REGISTRY="ghcr.io/gbolabs/photos-index"

    echo "Pulling pre-built images from GHCR (tag: $IMAGE_TAG)..."
    echo "Note: This requires access to the GitHub Container Registry"
    echo ""

    SERVICES=("api" "web" "indexing-service" "cleaner-service" "metadata-service" "thumbnail-service")

    for service in "${SERVICES[@]}"; do
        echo "Pulling $service..."
        podman pull "$REGISTRY/$service:$IMAGE_TAG"
        # Tag as localhost for use with kube manifest
        podman tag "$REGISTRY/$service:$IMAGE_TAG" "localhost/photos-index-$service:latest"
    done

    echo ""
    echo "All images pulled and tagged successfully"
    podman images | grep photos-index
}

start_services() {
    echo "Starting services..."

    # Create directories if they don't exist
    mkdir -p "$PHOTOS_PATH"
    mkdir -p "$HOME_PICTURES_PATH"

    # Generate manifest with paths substituted
    TRAEFIK_CONFIG_PATH="$SCRIPT_DIR/traefik"
    sed -e "s|path: /tmp/photos|path: $PHOTOS_PATH|g" \
        -e "s|path: /tmp/home-pictures|path: $HOME_PICTURES_PATH|g" \
        -e "s|path: /tmp/traefik-config|path: $TRAEFIK_CONFIG_PATH|g" \
        "$MANIFEST" | podman kube play -

    echo ""
    echo "Services starting. Access:"
    echo "  Application:      http://localhost:8080 (via Traefik)"
    echo "  API:              http://localhost:8080/api (via Traefik)"
    echo "  Traefik Dashboard: http://localhost:8081"
    echo "  Jaeger UI:        http://localhost:16686"
    echo "  PostgreSQL:       localhost:5432"
    echo ""
    echo "Mounted directories:"
    echo "  /photos:                    $PHOTOS_PATH"
    echo "  /scan-targets/home-pictures: $HOME_PICTURES_PATH"
    echo ""
    echo "Run '$0 status' to check container status"
    echo "Run '$0 logs' to view logs"
}

stop_services() {
    echo "Stopping services (preserving data)..."

    # Stop the pod without removing volumes
    if podman pod exists photos-index 2>/dev/null; then
        podman pod stop photos-index 2>/dev/null || true
        podman pod rm photos-index 2>/dev/null || true
    fi

    # Remove ConfigMap and Secret (they'll be recreated on start)
    podman kube play --down "$MANIFEST" 2>/dev/null || true

    echo "Services stopped. Data preserved in volumes."
    echo "Run '$0 clean' to remove all data including volumes."
}

clean_services() {
    echo "Stopping services and removing ALL data..."

    # Full teardown including volumes
    podman kube play --down "$MANIFEST" 2>/dev/null || true

    # Remove any remaining volumes
    podman volume rm postgres-pvc 2>/dev/null || true
    podman volume rm photos-index-postgres-data 2>/dev/null || true

    # List and remove any photos-index related volumes
    for vol in $(podman volume ls -q | grep -E "photos-index|postgres-pvc" 2>/dev/null); do
        echo "Removing volume: $vol"
        podman volume rm "$vol" 2>/dev/null || true
    done

    echo "All services and data removed."
}

restart_services() {
    stop_services
    echo ""
    start_services
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
    pull)
        pull_images
        ;;
    start)
        start_services
        ;;
    stop)
        stop_services
        ;;
    restart)
        restart_services
        ;;
    clean)
        clean_services
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
