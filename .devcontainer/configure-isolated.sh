#!/bin/bash
# Configure isolated devcontainer (no host credentials)
# This script runs INSIDE the container

set -e

echo "[devcontainer] Configuring isolated environment..."

# Configure Podman if socket is available
if [[ -S "/var/run/podman.sock" ]]; then
    echo "[devcontainer] Podman socket available"
    echo 'alias podman="podman --remote"' >> ~/.bashrc 2>/dev/null || true
fi

# Check if git is configured
if [[ -z "$(git config --global user.name 2>/dev/null)" ]]; then
    echo ""
    echo "=========================================="
    echo "  Git not configured - please set up:"
    echo "=========================================="
    echo "  git config --global user.name 'Your Name'"
    echo "  git config --global user.email 'you@example.com'"
    echo ""
fi

# Check if gh is authenticated
if ! gh auth status &> /dev/null; then
    echo ""
    echo "=========================================="
    echo "  GitHub CLI not authenticated"
    echo "=========================================="
    echo "  Run: gh auth login"
    echo "  (Use 'Login with a web browser' for device flow)"
    echo ""
fi

# Check if Claude is configured
if [[ ! -f "$HOME/.claude/settings.json" ]]; then
    echo ""
    echo "=========================================="
    echo "  Claude Code not configured"
    echo "=========================================="
    echo "  Run: claude"
    echo "  (Will prompt for API key on first run)"
    echo ""
fi

echo "[devcontainer] Isolated environment ready!"
echo "[devcontainer] See messages above if setup is needed."
