#!/bin/bash
# Configure Podman inside the container after start
# This script runs INSIDE the container

set -e

echo "[devcontainer] Configuring Podman connection..."

# Check if socket is mounted and accessible
if [[ -S "/var/run/podman.sock" ]]; then
    echo "[devcontainer] Podman socket available at /var/run/podman.sock"

    # Test connection
    if podman --remote info &> /dev/null; then
        echo "[devcontainer] Podman connection successful!"
        podman --remote version
    else
        echo "[devcontainer] Podman socket mounted but connection failed"
        echo "[devcontainer] You may need to check socket permissions"
    fi
else
    echo "[devcontainer] WARNING: Podman socket not mounted"
    echo "[devcontainer] Container operations will not work"
fi

# Create alias for convenience
echo 'alias podman="podman --remote"' >> ~/.bashrc 2>/dev/null || true

# Configure GitHub CLI token as environment variable
echo "[devcontainer] Configuring GitHub CLI..."
if gh auth status &> /dev/null; then
    echo "[devcontainer] GitHub CLI authenticated!"
    # Export GH_TOKEN for tools that need it
    echo 'export GH_TOKEN=$(gh auth token 2>/dev/null)' >> ~/.bashrc
    echo "[devcontainer] GH_TOKEN will be available in new shells"
else
    echo "[devcontainer] WARNING: GitHub CLI not authenticated"
    echo "[devcontainer] Run 'gh auth login' on your host machine"
fi
