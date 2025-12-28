# ADR-009: Watchtower for Automated Container Updates

**Status**: Accepted
**Date**: 2025-12-28
**Author**: Claude Code

## Context

Currently, deploying new versions to NAS devices (TrueNAS and Synology) requires manual intervention:

1. Release is tagged on GitHub
2. CI builds Docker images and pushes to GHCR
3. User must SSH to each NAS and run `docker compose pull`
4. User must restart containers with `docker compose up -d`

This manual process is error-prone and delays updates. We evaluated four options:

| Option | Complexity | Security | Latency |
|--------|------------|----------|---------|
| **Watchtower** | Low | Good (outbound only) | ~5 min |
| GitHub Actions + SSH | Medium | Poor (SSH keys in GitHub) | Instant |
| Webhook + Local Script | High | Medium (webhook exposure) | Instant |
| ArgoCD/Flux | Very High | Good | ~1 min |

## Decision

Use **Watchtower** for automated container updates on NAS devices.

Watchtower is a container that monitors running containers and automatically updates them when new images are available in the registry. It:

1. Runs alongside application containers
2. Polls GHCR every 5 minutes (configurable)
3. Compares local image digests with remote
4. Pulls new images and recreates containers when updates are detected
5. Cleans up old images

### Configuration

Add to each NAS compose file:

```yaml
watchtower:
  image: containrrr/watchtower
  container_name: watchtower
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock
  environment:
    WATCHTOWER_POLL_INTERVAL: 300        # Check every 5 minutes
    WATCHTOWER_CLEANUP: "true"           # Remove old images
    WATCHTOWER_INCLUDE_STOPPED: "true"   # Update stopped containers too
    WATCHTOWER_LABEL_ENABLE: "true"      # Only update labeled containers
  restart: unless-stopped
```

Add labels to services that should be auto-updated:

```yaml
services:
  api:
    labels:
      - com.centurylinklabs.watchtower.enable=true
```

### Selective Updates

Using `WATCHTOWER_LABEL_ENABLE: "true"` ensures only explicitly labeled containers are updated. This prevents accidental updates to:
- Infrastructure containers (Traefik, PostgreSQL)
- Third-party services with their own update cycles

## Consequences

### Positive

- **Simple setup**: Single container, no external infrastructure
- **No network exposure**: NAS initiates outbound connections only
- **Works behind NAT**: No port forwarding or webhooks required
- **Automatic cleanup**: Old images removed to save disk space
- **Graceful restarts**: Containers recreated with same configuration
- **Private registry support**: Works with GHCR authentication

### Negative

- **Polling delay**: Up to 5 minutes between release and deployment (acceptable for home lab)
- **All-or-nothing updates**: Cannot do staged rollouts across NAS devices
- **No built-in rollback**: Manual intervention required if update fails
- **Docker socket access**: Watchtower needs privileged access to Docker

### Mitigations

For rollback scenarios, maintain a manual procedure:
```bash
# On NAS, rollback to previous version
docker compose down
# Edit compose file to pin specific version
docker compose up -d
```

## References

- [Watchtower Documentation](https://containrrr.dev/watchtower/)
- [GHCR Authentication for Watchtower](https://containrrr.dev/watchtower/private-registries/)
- [Auto-deploy ideas document](../deployment/auto-deploy-idea.md)
