#!/bin/bash
# Run Claude Code in a sandboxed Podman container
#
# Simplified Usage:
#   ./scripts/claude-sandbox.sh --otel clone          # Default: persistent Seq + main branch
#   ./scripts/claude-sandbox.sh --otel --no-persist clone  # Ephemeral Seq
#   ./scripts/claude-sandbox.sh --help                # Show full help
#
# Features:
#   - Seq OTel receiver with persistence control
#   - Simple branch specification
#   - Clean, focused interface

set -euo pipefail

# Default values when no parameters are given
if [[ $# -eq 0 ]]; then
    set -- --otel clone
fi

# Show help message
show_help() {
    cat << EOF
Claude Sandbox Script - Simplified Development Environment

Usage:
  ./scripts/claude-sandbox.sh [options] [clone|mount]
  ./scripts/claude-sandbox.sh          # Default: --otel clone

Options:
  --otel             Enable OpenTelemetry logging with Seq
  --no-persist       Disable Seq volume persistence (ephemeral mode)
  --branch=BRANCH    Branch to checkout in clone mode [default: main]
  --skip-scope-check Skip GitHub token scope validation
  -h, --help         Show this help message

Modes:
  clone              Clone repo fresh inside container (uses 'main' branch)
  mount              Mount current directory (faster, changes persist)

Examples:
  # Default: Seq with persistence + main branch
  ./scripts/claude-sandbox.sh --otel clone

  # Ephemeral Seq (no persistence)
  ./scripts/claude-sandbox.sh --otel --no-persist clone

  # Specific branch
  ./scripts/claude-sandbox.sh --otel --branch=feature-branch clone

  # Just mount current directory
  ./scripts/claude-sandbox.sh mount

  # Rebuild the Docker image
  ./scripts/claude-sandbox.sh build

  # Pass -h to Claude Code CLI for help
  ./scripts/claude-sandbox.sh -h
  ./scripts/claude-sandbox.sh help

Persistence:
  With --otel (default): Logs persist in 'seq-data' volume across runs
  With --otel --no-persist: Logs are ephemeral (lost on container restart)
  Without --otel: No OTel logging

Cleanup:
  Remove persistent Seq data: podman volume rm seq-data
EOF
}

# Parse flags
OTEL_ENABLED=false
SKIP_SCOPE_CHECK=false
SEQ_PERSIST=true   # Default to persistent (with volume)
BRANCH="main"      # Default to main branch
POSITIONAL_ARGS=()

for arg in "$@"; do
    case "$arg" in
        --otel)
            OTEL_ENABLED=true
            ;;
        --no-persist)
            SEQ_PERSIST=false
            ;;
        --skip-scope-check)
            SKIP_SCOPE_CHECK=true
            ;;
        --branch=*)
            BRANCH="${arg#*=}"
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            POSITIONAL_ARGS+=("$arg")
            ;;
    esac
done
set -- "${POSITIONAL_ARGS[@]:-}"

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

# Start Seq for OTel logging
start_seq() {
    log "Starting Seq for OTel logging..."

    # Remove existing container if present
    podman rm -f seq-otel 2>/dev/null || true

    local volume_args=""
    local persist_msg=""

    if [[ "$SEQ_PERSIST" == "true" ]]; then
        # Create volume for persistent storage if it doesn't exist
        if ! podman volume exists seq-data; then
            podman volume create seq-data
        fi
        volume_args="-v seq-data:/data"
        persist_msg="(persistent)"
    else
        persist_msg="(ephemeral)"
    fi

        # Start Seq (port 80 serves both UI and OTLP ingestion)
        podman run -d --rm \
            --name seq-otel \
            -p 5341:80 \
            -e ACCEPT_EULA=Y \
            $volume_args \
            datalust/seq:latest

    log "Seq Dashboard: http://localhost:5341 $persist_msg"
    log "Seq OTLP Endpoint: http://host.containers.internal:5341/ingest/otlp (http/protobuf)"
}

# Get OTel environment variables for podman
get_otel_env_args() {
    if [[ "$OTEL_ENABLED" == "true" ]]; then
        echo "-e CLAUDE_CODE_ENABLE_TELEMETRY=1 \
              -e OTEL_LOG_USER_PROMPTS=1 \
              -e OTEL_LOGS_EXPORTER=otlp \
              -e OTEL_EXPORTER_OTLP_ENDPOINT=http://host.containers.internal:5341/ingest/otlp \
              -e OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf"
    fi
}

# Required GitHub token scopes for full functionality
REQUIRED_SCOPES="repo,workflow"

# Check if token has required scopes
check_token_scopes() {
    local scopes
    # Use sed instead of grep -oP for macOS compatibility
    scopes=$(gh auth status 2>&1 | grep "Token scopes:" | sed "s/.*Token scopes: //" || echo "")

    if [[ -z "$scopes" ]]; then
        warn "Could not determine token scopes"
        return 1
    fi

    log "Current token scopes: $scopes"

    # Check for workflow scope (needed for pushing workflow files)
    if [[ "$scopes" != *"workflow"* ]]; then
        warn "Token missing 'workflow' scope (needed to push .github/workflows changes)"
        return 1
    fi

    return 0
}

# Get GitHub token interactively
get_gh_token() {
    if [[ -n "${GH_TOKEN:-}" ]]; then
        log "Using GH_TOKEN from environment"
        return 0
    fi

    if command -v gh >/dev/null 2>&1; then
        if gh auth status >/dev/null 2>&1; then
            # Check if token has required scopes (unless skipped)
            if [[ "$SKIP_SCOPE_CHECK" != "true" ]]; then
                if ! check_token_scopes; then
                    warn "Token missing required scopes. Refreshing..."
                    gh auth refresh -h github.com -s workflow
                fi
            else
                log "Skipping scope check (--skip-scope-check)"
            fi

            log "Getting GitHub token from gh CLI..."
            GH_TOKEN=$(gh auth token)
            export GH_TOKEN
            log "GitHub token obtained successfully"
            return 0
        else
            warn "gh CLI not authenticated. Run 'gh auth login -s workflow' first"
        fi
    else
        warn "gh CLI not installed"
    fi

    echo ""
    echo -e "${YELLOW}GitHub token not available.${NC}"
    echo "Options:"
    echo "  1. Run 'gh auth login -s workflow' and re-run this script"
    echo "  2. Set GH_TOKEN environment variable (must include workflow scope)"
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

    # Get OTel environment variables
    local otel_args=""
    if [[ "$OTEL_ENABLED" == "true" ]]; then
        otel_args=$(get_otel_env_args)
    fi

    # shellcheck disable=SC2086
    podman run -it --rm \
        --name "$CONTAINER_NAME" \
        -e GH_TOKEN="${GH_TOKEN:-}" \
        -e GIT_AUTHOR_NAME="$git_name" \
        -e GIT_AUTHOR_EMAIL="$git_email" \
        -e GIT_COMMITTER_NAME="$git_name" \
        -e GIT_COMMITTER_EMAIL="$git_email" \
        -p 8443:8443 \
        -v "$(pwd):/workspace:Z" \
        $otel_args \
        "$IMAGE_NAME" \
        "$@"
}

# Run container with cloned source
run_clone_mode() {
    log "Running in CLONE mode (fresh clone inside container)"

    local git_name=$(git config user.name 2>/dev/null || echo "Claude Agent")
    local git_email=$(git config user.email 2>/dev/null || echo "claude@localhost")
    local branch="$BRANCH"

    # Get OTel environment variables
    local otel_args=""
    if [[ "$OTEL_ENABLED" == "true" ]]; then
        otel_args=$(get_otel_env_args)
    fi

    # shellcheck disable=SC2086
    podman run -it --rm \
        --name "$CONTAINER_NAME" \
        -e GH_TOKEN="${GH_TOKEN:-}" \
        -e GIT_AUTHOR_NAME="$git_name" \
        -e GIT_AUTHOR_EMAIL="$git_email" \
        -e GIT_COMMITTER_NAME="$git_name" \
        -e GIT_COMMITTER_EMAIL="$git_email" \
        -e REPO_URL="$REPO_URL" \
        -e BRANCH="$branch" \
        -p 8443:8443 \
        $otel_args \
        "$IMAGE_NAME" \
        "$@"
}

# Main
main() {
    # Handle -h or help flags to pass to Claude Code CLI
    if [[ "$1" == "-h" ]] || [[ "$1" == "help" ]]; then
        # Build image if needed, then pass -h to Claude Code CLI
        if ! podman image exists "$IMAGE_NAME"; then
            build_image
        else
            log "Using existing image"
        fi
        shift || true  # Remove -h/help argument
        run_mount_mode "$@"
        return
    fi
    
    check_prereqs

    # Build image if it doesn't exist
    if ! podman image exists "$IMAGE_NAME"; then
        build_image
    else
        log "Using existing image (run 'podman rmi $IMAGE_NAME' to rebuild)"
    fi

    # Start Seq if OTel is enabled
    if [[ "$OTEL_ENABLED" == "true" ]]; then
        start_seq
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
