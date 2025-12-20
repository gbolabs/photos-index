# Claude Code Sandbox

Run Claude Code in an isolated Podman container with YOLO mode enabled. This provides a safe environment where Claude has full permissions without risking your host system.

## Why Use a Sandbox?

- **Security**: Claude can't access files outside the container
- **YOLO Mode**: All permissions pre-granted, no prompts
- **Reproducible**: Fresh environment each time
- **GitHub Access**: `gh` CLI configured for PRs

## Prerequisites

```bash
# Required
export ANTHROPIC_API_KEY="your-api-key"

# For GitHub CLI (PRs, issues)
export GH_TOKEN="your-github-token"

# Podman installed
podman --version
```

## Quick Start

```bash
# Make script executable
chmod +x scripts/claude-sandbox.sh

# Run with mounted source (changes persist)
./scripts/claude-sandbox.sh mount

# Run with fresh clone (fully isolated)
./scripts/claude-sandbox.sh clone
```

## Modes

### Mount Mode (Default)

Mounts your current directory into the container. Changes made by Claude persist on your host.

```bash
./scripts/claude-sandbox.sh mount
```

**Pros**: Fast, no re-clone needed, changes persist
**Cons**: Claude can modify your local files

### Clone Mode

Clones the repo fresh inside the container. Changes only persist if pushed to remote.

```bash
./scripts/claude-sandbox.sh clone
```

**Pros**: Fully isolated, can't break local files
**Cons**: Must push changes to keep them

## What's Included

The container image includes:

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0 | Backend development |
| Node.js | 24 | Frontend development |
| Claude Code | Latest | AI assistant |
| GitHub CLI | Latest | PR/issue management |
| Git | Latest | Version control |

## YOLO Mode Settings

The container has these permissions pre-configured in `/root/.claude/settings.json`:

```json
{
  "permissions": {
    "allow": [
      "Bash(*)",
      "Read(*)",
      "Write(*)",
      "Edit(*)",
      "Glob(*)",
      "Grep(*)",
      "WebFetch(*)",
      "WebSearch(*)",
      "Task(*)",
      "TodoWrite(*)",
      "NotebookEdit(*)"
    ],
    "deny": []
  }
}
```

## Usage Examples

### Run a Specific Task

```bash
# Mount mode with initial prompt
./scripts/claude-sandbox.sh mount "implement Agent 1 task from parallel-development-plan.md"

# Clone mode for isolated work
./scripts/claude-sandbox.sh clone "create feature branch and implement file scanner"
```

### Rebuild the Image

```bash
./scripts/claude-sandbox.sh build
```

### Custom Commands

```bash
# Pass additional arguments to claude
./scripts/claude-sandbox.sh mount --model opus "review the code"
```

## Running Multiple Agents

For parallel development, run multiple containers:

```bash
# Terminal 1 - Agent 1
CONTAINER_SUFFIX=agent1 ./scripts/claude-sandbox.sh clone "implement ScanDirectories API"

# Terminal 2 - Agent 2
CONTAINER_SUFFIX=agent2 ./scripts/claude-sandbox.sh clone "implement IndexedFiles API"
```

## Troubleshooting

### "Permission denied" on mounted files

Use the `:Z` SELinux label (already included) or run:

```bash
podman run --security-opt label=disable ...
```

### GitHub CLI not authenticated

Ensure `GH_TOKEN` is set before running:

```bash
export GH_TOKEN=$(gh auth token)
./scripts/claude-sandbox.sh mount
```

### Container won't start

Remove any existing container:

```bash
podman rm -f claude-sandbox
```

## Security Notes

1. **API Keys**: Passed as environment variables, not stored in image
2. **Network**: Container has full network access (needed for API calls)
3. **Filesystem**: Only `/workspace` is accessible (mounted or cloned)
4. **No root on host**: Container runs as root inside but can't escalate on host

## Customization

### Add More Tools

Edit the Dockerfile in `scripts/claude-sandbox.sh`:

```dockerfile
RUN apt-get install -y your-tool
```

### Change Permissions

Modify the settings.json section to restrict permissions:

```json
{
  "permissions": {
    "allow": ["Read(*)", "Grep(*)"],
    "deny": ["Bash(rm *)", "Write(/etc/*)"]
  }
}
```
