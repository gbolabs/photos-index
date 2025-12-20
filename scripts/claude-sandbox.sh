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
#   - ANTHROPIC_API_KEY environment variable set
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

# Check prerequisites
check_prereqs() {
    log "Checking prerequisites..."

    command -v podman >/dev/null 2>&1 || error "Podman is not installed"

    if [[ -z "${ANTHROPIC_API_KEY:-}" ]]; then
        error "ANTHROPIC_API_KEY is not set"
    fi

    if [[ -z "${GH_TOKEN:-}" ]]; then
        warn "GH_TOKEN is not set - gh CLI won't work"
    fi

    log "Prerequisites OK"
}

# Build the container image
build_image() {
    log "Building container image..."

    podman build -t "$IMAGE_NAME" -f - . <<'DOCKERFILE'
FROM mcr.microsoft.com/dotnet/sdk:10.0

# Install Node.js 24
RUN curl -fsSL https://deb.nodesource.com/setup_24.x | bash - && \
    apt-get install -y nodejs

# Install tools
RUN apt-get update && apt-get install -y \
    git \
    curl \
    jq \
    vim \
    && rm -rf /var/lib/apt/lists/*

# Install GitHub CLI
RUN curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg | dd of=/usr/share/keyrings/githubcli-archive-keyring.gpg && \
    chmod go+r /usr/share/keyrings/githubcli-archive-keyring.gpg && \
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" | tee /etc/apt/sources.list.d/github-cli.list > /dev/null && \
    apt-get update && \
    apt-get install -y gh && \
    rm -rf /var/lib/apt/lists/*

# Install Claude Code CLI
RUN npm install -g @anthropic-ai/claude-code

# Create workspace
WORKDIR /workspace

# Create Claude settings directory
RUN mkdir -p /root/.claude

# YOLO mode settings - allow all tools without prompts
RUN echo '{ \
  "permissions": { \
    "allow": [ \
      "Bash(*)", \
      "Read(*)", \
      "Write(*)", \
      "Edit(*)", \
      "Glob(*)", \
      "Grep(*)", \
      "WebFetch(*)", \
      "WebSearch(*)", \
      "Task(*)", \
      "TodoWrite(*)", \
      "NotebookEdit(*)" \
    ], \
    "deny": [] \
  }, \
  "enableAllProjectMcpServers": true \
}' > /root/.claude/settings.json

# Entrypoint script
RUN echo '#!/bin/bash\n\
echo "🤖 Claude Code Sandbox"\n\
echo "======================"\n\
echo "Mode: YOLO (all permissions granted)"\n\
echo ""\n\
if [[ -n "${GH_TOKEN:-}" ]]; then\n\
    echo "✅ GitHub CLI authenticated"\n\
else\n\
    echo "⚠️  GitHub CLI not authenticated (GH_TOKEN not set)"\n\
fi\n\
echo ""\n\
echo "Starting Claude Code..."\n\
echo ""\n\
exec claude "$@"\n\
' > /entrypoint.sh && chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
DOCKERFILE

    log "Image built successfully"
}

# Run container with mounted source
run_mount_mode() {
    log "Running in MOUNT mode (current directory mounted)"

    local git_name=$(git config user.name 2>/dev/null || echo "Claude Agent")
    local git_email=$(git config user.email 2>/dev/null || echo "claude@localhost")

    podman run -it --rm \
        --name "$CONTAINER_NAME" \
        -e ANTHROPIC_API_KEY="$ANTHROPIC_API_KEY" \
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
        -e ANTHROPIC_API_KEY="$ANTHROPIC_API_KEY" \
        -e GH_TOKEN="${GH_TOKEN:-}" \
        -e GIT_AUTHOR_NAME="$git_name" \
        -e GIT_AUTHOR_EMAIL="$git_email" \
        -e GIT_COMMITTER_NAME="$git_name" \
        -e GIT_COMMITTER_EMAIL="$git_email" \
        -e REPO_URL="$REPO_URL" \
        -e BRANCH="$branch" \
        "$IMAGE_NAME" \
        --init-command "git clone \$REPO_URL . && git checkout \$BRANCH" \
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
