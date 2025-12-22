# Integration Tests Setup

This guide explains how to run integration tests that use Testcontainers to spin up real PostgreSQL instances.

## Prerequisites

Integration tests require a container runtime (Docker or Podman) accessible via the Docker socket.

## Running Integration Tests

```bash
cd /workspace/src
dotnet test PhotosIndex.sln --filter "Integration"
```

## Local Development (Native)

If running directly on your machine with Docker or Podman installed:

```bash
# Docker (default)
# No additional setup needed - uses /var/run/docker.sock

# Podman
systemctl --user start podman.socket
export DOCKER_HOST=unix:///run/user/$(id -u)/podman/podman.sock
```

## WSL2 with Podman

When using Podman on WSL2:

```bash
# 1. Start the Podman socket service
systemctl --user start podman.socket

# 2. Enable it to start on boot (optional)
systemctl --user enable podman.socket

# 3. Verify the socket exists
ls -la /run/user/$(id -u)/podman/podman.sock
```

## Development Containers (Claude Code, VS Code, etc.)

When running inside a development container and the host has Podman:

### Option 1: Mount Socket at Container Start

```bash
# Get your user ID on the host
HOST_UID=$(id -u)

# Start container with socket mounted
docker run -it \
  -v /run/user/$HOST_UID/podman/podman.sock:/var/run/docker.sock \
  -v /workspace:/workspace \
  your-dev-container

# Or with Podman
podman run -it \
  -v /run/user/$HOST_UID/podman/podman.sock:/var/run/docker.sock:Z \
  -v /workspace:/workspace \
  your-dev-container
```

### Option 2: Using Docker Compose

Add to your `docker-compose.yml`:

```yaml
services:
  dev:
    image: your-dev-container
    volumes:
      - /run/user/1000/podman/podman.sock:/var/run/docker.sock
      - .:/workspace
```

### Option 3: Using Podman Machine (macOS/Windows)

```bash
# Initialize and start Podman machine
podman machine init
podman machine start

# The socket is automatically available at the default location
```

## Troubleshooting

### "Docker is not running" Error

```bash
# Check if socket exists
ls -la /var/run/docker.sock

# Check socket permissions
stat /var/run/docker.sock

# Test connectivity
curl --unix-socket /var/run/docker.sock http://localhost/version
```

### Permission Denied

```bash
# Add user to docker group (Docker)
sudo usermod -aG docker $USER

# For Podman, ensure socket is running as your user
systemctl --user status podman.socket
```

### Testcontainers Configuration

If the socket is at a non-standard location, set the environment variable:

```bash
export DOCKER_HOST=unix:///path/to/your/socket
```

Or create `~/.testcontainers.properties`:

```properties
docker.host=unix:///path/to/your/socket
```

## Test Categories

| Test Suite | Description | Container Required |
|------------|-------------|-------------------|
| Unit Tests | Mock-based tests | No |
| Integration Tests | Real database tests | Yes (PostgreSQL) |
| E2E Tests | Full stack tests | Yes (all services) |

### Running Only Unit Tests

```bash
dotnet test PhotosIndex.sln --filter "FullyQualifiedName!~Integration"
```

### Running Only Integration Tests

```bash
dotnet test PhotosIndex.sln --filter "Integration"
```

## CI/CD

GitHub Actions workflows use Docker-in-Docker (DinD) for integration tests. See `.github/workflows/ci.yml` for the configuration.
