# 006: Claude Code OTel Prompt Logging

**Status**: 🔲 Not Started
**Priority**: P2
**Effort**: 1-2 hours

## Objective

Enable OpenTelemetry logging for Claude Code prompts, allowing developers to view all prompts and tool calls in the Aspire Dashboard for debugging and audit purposes.

## Problem Statement

When running Claude Code in sandbox or devcontainer mode, there's no visibility into:
- What prompts were sent to the model
- What tool calls were made
- The sequence of operations

This makes debugging difficult and prevents auditing of AI-assisted development sessions.

## Solution

Add opt-in OTel logging to both the sandbox script and devcontainer:
- **Sandbox**: `--otel` flag starts Aspire Dashboard and configures OTel
- **Devcontainer**: `source .devcontainer/start-claude-otel.sh` script

## Implementation

### Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `scripts/claude-sandbox.sh` | Modify | Add `--otel` flag and Aspire startup |
| `scripts/entrypoint.sh` | Modify | Display OTel status in banner |
| `.devcontainer/start-claude-otel.sh` | Create | Script to enable OTel in devcontainer |
| `docs/claude-sandbox.md` | Modify | Add Observability section |

### Environment Variables

| Variable | Value | Purpose |
|----------|-------|---------|
| `CLAUDE_CODE_ENABLE_TELEMETRY` | `1` | Enable Claude Code telemetry |
| `OTEL_LOG_USER_PROMPTS` | `1` | Log actual prompt content |
| `OTEL_LOGS_EXPORTER` | `otlp` | Export to OTLP endpoint |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://...:18889` | Aspire gRPC endpoint |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` | Use gRPC protocol |

## Usage

### Sandbox Mode

```bash
# With OTel enabled
./scripts/claude-sandbox.sh --otel mount

# View prompts
open http://localhost:18888
```

### Devcontainer

```bash
# Enable OTel (sets env vars for current shell)
source .devcontainer/start-claude-otel.sh

# Run Claude Code
claude

# View prompts
open http://localhost:18888
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

## Acceptance Criteria

- [ ] `--otel` flag recognized by sandbox script
- [ ] Aspire Dashboard starts when `--otel` is used
- [ ] Claude Code sends telemetry to Aspire
- [ ] Prompts visible in Aspire Dashboard logs
- [ ] No Aspire container starts without `--otel`
- [ ] Devcontainer script exports correct env vars
- [ ] Documentation updated

## Testing

1. Run `./scripts/claude-sandbox.sh --otel mount`
2. Verify Aspire Dashboard starts at http://localhost:18888
3. Send prompts to Claude Code
4. Verify prompts appear in Aspire Dashboard logs
5. Run without `--otel` and verify no Aspire container starts

## Related

- `08-observability/` - Application OTel setup
- `scripts/claude-sandbox.sh` - YOLO sandbox script
- `.devcontainer/` - Dev container configuration
