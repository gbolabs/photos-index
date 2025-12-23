#!/bin/bash
# Start Aspire Dashboard and configure Claude Code OTel
#
# Usage: source .devcontainer/start-claude-otel.sh
#
# This script:
# 1. Starts an Aspire Dashboard container for receiving OTel data
# 2. Sets environment variables for Claude Code telemetry
# 3. Displays URLs for accessing the dashboard

set -euo pipefail

echo "Starting Aspire Dashboard for Claude Code OTel..."

# Remove existing container if present
podman rm -f aspire-claude-otel 2>/dev/null || true

# Start Aspire Dashboard
podman run -d --rm \
    --name aspire-claude-otel \
    -p 18888:18888 \
    -p 18889:18889 \
    -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
    mcr.microsoft.com/dotnet/aspire-dashboard:9.1

# Export OTel environment variables
export CLAUDE_CODE_ENABLE_TELEMETRY=1
export OTEL_LOG_USER_PROMPTS=1
export OTEL_LOGS_EXPORTER=otlp
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:18889
export OTEL_EXPORTER_OTLP_PROTOCOL=grpc

echo ""
echo "OTel environment configured:"
echo "  CLAUDE_CODE_ENABLE_TELEMETRY=1"
echo "  OTEL_LOG_USER_PROMPTS=1"
echo "  OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:18889"
echo ""
echo "Aspire Dashboard: http://localhost:18888"
echo ""
echo "Run 'claude' to start Claude Code with OTel enabled"
