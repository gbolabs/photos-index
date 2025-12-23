# VS Code Dev Container

Development environment for Photos Index using VS Code Remote Containers.

> **Note**: This is for **interactive development** with VS Code.
> For **autonomous agent execution** (YOLO mode), use [`scripts/claude-sandbox.sh`](../docs/claude-sandbox.md) instead.

## Quick Start

1. Install [VS Code](https://code.visualstudio.com/) and the [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
2. Start Podman: `podman machine start`
3. Open repo in VS Code → `Cmd/Ctrl+Shift+P` → "Dev Containers: Reopen in Container"

## Available Configurations

| Configuration | Description |
|--------------|-------------|
| **Photos Index Dev** | Inherits host credentials (git, gh, claude) |
| **Photos Index Dev (Isolated)** | Fresh environment, prompts for login |

## What's Included

- .NET 10 SDK
- Node.js 24 with Angular CLI
- GitHub CLI (authenticated via host or device login)
- Claude Code CLI
- Podman (connected to host)

## Host Mounts (Default Config)

| Host Path | Container Path | Purpose |
|-----------|---------------|---------|
| `~/.gitconfig` | `/home/vscode/.gitconfig` | Git configuration |
| `~/.config/gh/` | `/home/vscode/.config/gh/` | GitHub CLI auth |
| `~/.claude/` | `/home/vscode/.claude/` | Claude Code settings |
| Podman socket | `/var/run/podman.sock` | Container access |

## Network Access

- TrueNAS host: `192.168.114.31` (accessible as `truenas.local`)
- Ports 80/443 available

## Forwarded Ports

| Port | Service |
|------|---------|
| 4200 | Angular dev server |
| 5000 | API (HTTP) |
| 5001 | API (HTTPS) |
| 5432 | PostgreSQL |
| 18888 | Aspire Dashboard |
