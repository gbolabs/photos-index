# 005: VS Code Dev Container Setup

**Status**: ✅ Complete
**PR**: [#80](https://github.com/gbolabs/photos-index/pull/80)
**Priority**: P2
**Effort**: 2-3 hours

## Objective

Provide a consistent, containerized development environment for VS Code users with all required tools pre-installed and host integrations for Podman, GitHub CLI, and Claude Code.

## Problem Statement

Setting up the development environment manually requires installing multiple tools:
- .NET 10 SDK
- Node.js 24
- Angular CLI
- GitHub CLI
- Claude Code CLI
- Podman

This creates friction for new contributors and can lead to environment inconsistencies.

## Solution

Implement VS Code Dev Containers with two configurations:

1. **Default**: Inherits host credentials (git, gh, claude) for seamless development
2. **Isolated**: Fresh environment with device login prompts for testing or separate accounts

## Implementation

### Files Created

| File | Purpose |
|------|---------|
| `.devcontainer/devcontainer.json` | Main dev container configuration |
| `.devcontainer/Dockerfile` | Container image with all dev tools |
| `.devcontainer/isolated/devcontainer.json` | Isolated mode configuration |
| `.devcontainer/setup-podman-socket.sh` | Cross-platform Podman socket detection |
| `.devcontainer/configure-podman.sh` | In-container Podman + gh setup |
| `.devcontainer/configure-isolated.sh` | Isolated mode setup prompts |
| `.devcontainer/README.md` | Dev container documentation |

### Features

- **.NET 10 SDK**: Backend development
- **Node.js 24 + Angular CLI**: Frontend development
- **GitHub CLI**: PR and issue management (authenticated via host mount)
- **Claude Code CLI**: AI-assisted development (settings inherited from host)
- **Podman**: Container-in-container workflows via host socket
- **Network access**: TrueNAS host (192.168.114.31:80,443)

### Host Mounts (Default Config)

| Host Path | Container Path | Purpose |
|-----------|---------------|---------|
| `~/.gitconfig` | `/home/vscode/.gitconfig` | Git identity |
| `~/.config/gh/` | `/home/vscode/.config/gh/` | GitHub CLI auth |
| `~/.claude/` | `/home/vscode/.claude/` | Claude Code settings |
| Podman socket | `/var/run/podman.sock` | Container access |

### Cross-Platform Support

The `setup-podman-socket.sh` script detects the host OS and locates the Podman socket:
- **macOS**: `~/.local/share/containers/podman/machine/podman.sock`
- **Windows**: `//./pipe/podman-machine-default`
- **Linux**: `/run/user/$UID/podman/podman.sock`

## Documentation Updates

- `CONTRIBUTING.md`: Added dev container instructions as recommended setup option
- `docs/claude-sandbox.md`: Added comparison table with devcontainer

## Acceptance Criteria

- [x] Dev container builds successfully
- [x] .NET, Node.js, Angular CLI available
- [x] GitHub CLI authenticated (default config)
- [x] Claude Code settings inherited (default config)
- [x] Podman socket accessible
- [x] Isolated mode prompts for login
- [x] Documentation updated
- [x] Cross-platform socket detection

## Related

- `scripts/claude-sandbox.sh` - Autonomous YOLO mode (different use case)
- `docs/claude-sandbox.md` - Sandbox documentation
