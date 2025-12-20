#!/bin/bash
# Run Claude Code in a sandboxed Podman container with YOLO mode
#
# Usage:
#   ./scripts/claude-sandbox.sh [clone|mount]
#
# Modes:
#   clone  - Clone repo fresh inside container (safer, isolated)
#   mount  - Mount current directory (faster, changes persist)
#
# Prerequisites:
#   - Podman installed
#   - GH_TOKEN environment variable set (for gh CLI)
#   - Git configured with user.name and user.email

set -euo pipefail

MODE="${1:-mount}"
REPO_URL="https://github.com/gbolabs/photos-index.git"
CONTAINER_NAME="claude-sandbox"
IMAGE_NAME="claude-sandbox:latest"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log() { echo -e "${GREEN}[INFO]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

# Get GitHub token interactively
get_gh_token() {
    if [[ -n "${GH_TOKEN:-}" ]]; then
        log "Using GH_TOKEN from environment"
        return 0
    fi

    if command -v gh >/dev/null 2>&1; then
        if gh auth status >/dev/null 2>&1; then
            log "Getting GitHub token from gh CLI..."
            GH_TOKEN=$(gh auth token)
            export GH_TOKEN
            log "GitHub token obtained successfully"
            return 0
        else
            warn "gh CLI not authenticated. Run 'gh auth login' first"
        fi
    else
        warn "gh CLI not installed"
    fi

    echo ""
    echo -e "${YELLOW}GitHub token not available.${NC}"
    echo "Options:"
    echo "  1. Run 'gh auth login' and re-run this script"
    echo "  2. Set GH_TOKEN environment variable"
    echo "  3. Continue without GitHub CLI (some features won't work)"
    echo ""
    read -p "Continue without GitHub token? [y/N] " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
}

# Check prerequisites
check_prereqs() {
    log "Checking prerequisites..."

    command -v podman >/dev/null 2>&1 || error "Podman is not installed"

    # Interactive token setup
    get_gh_token

    log "Prerequisites OK"
}

# Build the container image
build_image() {
    log "Building container image..."

    local script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    local dockerfile="$script_dir/Dockerfile.claude-sandbox"

    if [[ ! -f "$dockerfile" ]]; then
        error "Dockerfile not found: $dockerfile"
    fi

    podman build -t "$IMAGE_NAME" -f "$dockerfile" "$script_dir/.."

    log "Image built successfully"
}

# Run container with mounted source
run_mount_mode() {
    log "Running in MOUNT mode (current directory mounted)"

    local git_name=$(git config user.name 2>/dev/null || echo "Claude Agent")
    local git_email=$(git config user.email 2>/dev/null || echo "claude@localhost")

    podman run -it --rm \
        --name "$CONTAINER_NAME" \
        -e GH_TOKEN="${GH_TOKEN:-}" \
        -e GIT_AUTHOR_NAME="$git_name" \
        -e GIT_AUTHOR_EMAIL="$git_email" \
        -e GIT_COMMITTER_NAME="$git_name" \
        -e GIT_COMMITTER_EMAIL="$git_email" \
        -v "$(pwd):/workspace:Z" \
        "$IMAGE_NAME" \
        "$@"
}

# Run container with cloned source
run_clone_mode() {
    log "Running in CLONE mode (fresh clone inside container)"

    local git_name=$(git config user.name 2>/dev/null || echo "Claude Agent")
    local git_email=$(git config user.email 2>/dev/null || echo "claude@localhost")
    local branch=$(git branch --show-current 2>/dev/null || echo "main")

    podman run -it --rm \
        --name "$CONTAINER_NAME" \
        -e GH_TOKEN="${GH_TOKEN:-}" \
        -e GIT_AUTHOR_NAME="$git_name" \
        -e GIT_AUTHOR_EMAIL="$git_email" \
        -e GIT_COMMITTER_NAME="$git_name" \
        -e GIT_COMMITTER_EMAIL="$git_email" \
        -e REPO_URL="$REPO_URL" \
        -e BRANCH="$branch" \
        "$IMAGE_NAME" \
        "$@"
}

# Main
main() {
    check_prereqs

    # Build image if it doesn't exist
    if ! podman image exists "$IMAGE_NAME"; then
        build_image
    else
        log "Using existing image (run 'podman rmi $IMAGE_NAME' to rebuild)"
    fi

    shift || true  # Remove mode argument

    case "$MODE" in
        mount)
            run_mount_mode "$@"
            ;;
        clone)
            run_clone_mode "$@"
            ;;
        build)
            build_image
            ;;
        *)
            error "Unknown mode: $MODE (use 'mount', 'clone', or 'build')"
            ;;
    esac
}

main "$@"
