#!/bin/bash
# Setup script that runs on the HOST before container creation
# Detects OS and exports PODMAN_SOCK_PATH for the devcontainer

set -e

echo "[devcontainer] Detecting Podman socket path..."

# Detect OS
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS - get socket from podman machine
    if command -v podman &> /dev/null; then
        SOCK_PATH=$(podman machine inspect --format '{{.ConnectionInfo.PodmanSocket.Path}}' 2>/dev/null || true)
        if [[ -z "$SOCK_PATH" ]]; then
            # Fallback to default location
            SOCK_PATH="$HOME/.local/share/containers/podman/machine/podman.sock"
        fi
    else
        SOCK_PATH="$HOME/.local/share/containers/podman/machine/podman.sock"
    fi
    echo "[devcontainer] macOS detected, socket: $SOCK_PATH"

elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "cygwin" ]] || [[ -n "$WINDIR" ]]; then
    # Windows - use named pipe via WSL or npipe
    # The socket is typically exposed via podman machine in WSL2
    SOCK_PATH="//./pipe/podman-machine-default"
    echo "[devcontainer] Windows detected, socket: $SOCK_PATH"

else
    # Linux - socket in user runtime dir or default location
    if [[ -S "/run/user/$(id -u)/podman/podman.sock" ]]; then
        SOCK_PATH="/run/user/$(id -u)/podman/podman.sock"
    elif [[ -S "/run/podman/podman.sock" ]]; then
        SOCK_PATH="/run/podman/podman.sock"
    else
        SOCK_PATH="$HOME/.local/share/containers/podman/machine/podman.sock"
    fi
    echo "[devcontainer] Linux detected, socket: $SOCK_PATH"
fi

# Verify socket exists
if [[ -S "$SOCK_PATH" ]] || [[ "$SOCK_PATH" == //* ]]; then
    echo "[devcontainer] Podman socket found: $SOCK_PATH"
    export PODMAN_SOCK_PATH="$SOCK_PATH"
else
    echo "[devcontainer] WARNING: Podman socket not found at $SOCK_PATH"
    echo "[devcontainer] Make sure 'podman machine start' has been run"
    echo "[devcontainer] You can set PODMAN_SOCK_PATH manually if needed"
fi
