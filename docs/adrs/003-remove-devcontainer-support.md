# ADR-003: Remove Dev Container Support

**Status**: Accepted
**Date**: 2025-12-23
**Author**: Claude Code

## Context

The project implemented VS Code Dev Containers (PR #80) to provide a consistent development environment. However, the implementation has fundamental issues on macOS with Podman:

1. **Socket mounting fails**: macOS Podman uses Unix sockets in `/var/folders/` temp directories. These cannot be bind-mounted into containers via symlinks.
2. **Variable substitution issues**: The `${localEnv:VAR:default}` syntax with nested defaults is not reliably parsed by VS Code.
3. **Cross-platform complexity**: Each OS (macOS, Windows, Linux) requires different socket paths and connection methods.

Without the Podman socket, the devcontainer cannot run containers internally, defeating the purpose of having a containerized development environment for a project that relies heavily on container workflows.

## Decision

Remove all devcontainer support from the repository:

1. Delete `.devcontainer/` directory and all configuration files
2. Remove `.github/workflows/devcontainer.yml` CI workflow
3. Update `CONTRIBUTING.md` to remove devcontainer instructions
4. Archive the backlog item as deprecated

Developers should use the manual setup (Option 2 in CONTRIBUTING.md) or the `scripts/claude-sandbox.sh` script for isolated development.

## Consequences

### Positive

- Eliminates maintenance burden for non-functional feature
- Reduces confusion for new contributors
- Simplifies CI pipeline
- Manual setup with local tools is more reliable

### Negative

- No containerized development environment
- Contributors must install tools manually (.NET, Node.js, etc.)
- Potential for environment inconsistencies between developers

### Mitigations

- `scripts/setup-dev-env.sh` for automated dependency installation
- Clear manual setup instructions in CONTRIBUTING.md
- `local-dev.sh` script for running the full stack in containers
- `scripts/claude-sandbox.sh` for isolated Claude Code execution

## References

- Original implementation: PR #80
- VS Code devcontainer variable substitution: https://containers.dev/implementors/json_reference/
