# Plan: Claude Code OTel Logging with Aspire Dashboard

## Goal

Enable OpenTelemetry logging for Claude Code prompts, sending telemetry to an Aspire Dashboard container for visibility into all prompts and tool calls.

## Key Files to Modify

1. `scripts/claude-sandbox.sh` - Add Aspire container startup and OTel env vars
2. `scripts/entrypoint.sh` - Add OTel environment configuration
3. `.devcontainer/start-claude-otel.sh` - Script to enable OTel in devcontainer
4. `docs/claude-sandbox.md` - Update documentation
5. `docs/backlog/` - Create backlog item

## Implementation Steps

### Step 1: Update scripts/claude-sandbox.sh

Add `--otel` flag support to enable OTel logging:

```bash
# Parse --otel flag
OTEL_ENABLED=false
for arg in "$@"; do
    if [[ "$arg" == "--otel" ]]; then
        OTEL_ENABLED=true
    fi
done

start_aspire_dashboard() {
    log "Starting Aspire Dashboard for OTel..."

    # Remove existing if present
    podman rm -f aspire-otel 2>/dev/null || true

    # Start Aspire Dashboard
    podman run -d --rm \
        --name aspire-otel \
        -p 18888:18888 \
        -p 18889:18889 \
        -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
        mcr.microsoft.com/dotnet/aspire-dashboard:9.1

    log "Aspire Dashboard: http://localhost:18888"
}
```

Call `start_aspire_dashboard` only when `--otel` flag is present.

Add OTel environment variables to podman run commands (only when `--otel`):
- `CLAUDE_CODE_ENABLE_TELEMETRY=1`
- `OTEL_LOG_USER_PROMPTS=1`
- `OTEL_LOGS_EXPORTER=otlp`
- `OTEL_EXPORTER_OTLP_ENDPOINT=http://host.containers.internal:18889`
- `OTEL_EXPORTER_OTLP_PROTOCOL=grpc`

### Step 2: Update scripts/entrypoint.sh

Display OTel status in startup banner:

```bash
if [[ "${CLAUDE_CODE_ENABLE_TELEMETRY:-}" == "1" ]]; then
    echo "Telemetry: Enabled (OTel to ${OTEL_EXPORTER_OTLP_ENDPOINT:-not set})"
fi
```

### Step 3: Create devcontainer script

Create `.devcontainer/start-claude-otel.sh` script:

```bash
#!/bin/bash
# Start Aspire Dashboard and configure Claude Code OTel

podman run -d --rm --name aspire-claude-otel \
    -p 18888:18888 -p 18889:18889 \
    -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
    mcr.microsoft.com/dotnet/aspire-dashboard:9.1

export CLAUDE_CODE_ENABLE_TELEMETRY=1
export OTEL_LOG_USER_PROMPTS=1
export OTEL_LOGS_EXPORTER=otlp
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:18889
export OTEL_EXPORTER_OTLP_PROTOCOL=grpc

echo "Aspire Dashboard: http://localhost:18888"
echo "Run 'claude' to start Claude Code with OTel enabled"
```

User runs `source .devcontainer/start-claude-otel.sh` when they want OTel.

### Step 4: Create Backlog Item

Create `docs/backlog/06-ci-cd/006-claude-otel-logging.md` with full task specification.

### Step 5: Update Documentation

Add new section to `docs/claude-sandbox.md` after "Running Multiple Agents":

```markdown
## Observability (OTel)

Enable OpenTelemetry logging to capture all prompts and tool calls in the Aspire Dashboard.

### Usage

\`\`\`bash
# Start sandbox with OTel enabled
./scripts/claude-sandbox.sh --otel mount

# View prompts and tool calls
open http://localhost:18888
\`\`\`
```

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│ Host Machine                                            │
│                                                         │
│  ┌──────────────────┐     ┌──────────────────────────┐ │
│  │ Aspire Dashboard │◄────│ Claude Code Container    │ │
│  │ :18888 (UI)      │     │                          │ │
│  │ :18889 (OTLP)    │     │ OTEL_EXPORTER_OTLP_*     │ │
│  └──────────────────┘     │ CLAUDE_CODE_ENABLE_*     │ │
│                           │ OTEL_LOG_USER_PROMPTS=1  │ │
│                           └──────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

## Testing

1. Run `./scripts/claude-sandbox.sh --otel mount`
2. Verify Aspire Dashboard starts at http://localhost:18888
3. Send prompts to Claude Code
4. Verify prompts appear in Aspire Dashboard logs
5. Run without `--otel` and verify no Aspire container starts

## Notes

- Using `host.containers.internal` for Podman to reach host network
- Aspire Dashboard runs in anonymous mode (no auth) for dev convenience
- OTel is opt-in via environment variables
