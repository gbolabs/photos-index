# Traefik Configuration

This directory contains the external Traefik configuration files used by the Photos Index deployment.

## Files

- **traefik.yml** - Static configuration for Traefik including:
  - Entry points (web on port 8080, dashboard on port 8081)
  - File provider configuration
  - Logging settings
  - OpenTelemetry tracing configuration

- **dynamic.yml** - Dynamic configuration for Traefik including:
  - HTTP routers for API and Web services
  - Middleware for path prefix stripping
  - Service load balancer configurations

## Usage

These configuration files are mounted into the Traefik container via a hostPath volume. The volume is defined in the main [photos-index.yaml](../photos-index.yaml) manifest.

## Modifying Configuration

To modify the Traefik configuration:

1. Edit the appropriate file (traefik.yml for static config, dynamic.yml for dynamic config)
2. Restart the pod to apply changes:
   ```bash
   podman kube play --down photos-index.yaml
   podman kube play photos-index.yaml
   ```

Note: Dynamic configuration changes may be picked up automatically depending on the provider settings, but static configuration changes require a restart.
