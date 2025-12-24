# 008: Claude API Traffic Logging Proxy

**Status**: 🔲 Not Started
**Priority**: P2
**Effort**: 2-3 hours
**ADR**: [ADR-005](../../adrs/005-claude-api-traffic-logging.md)

## Objective

Implement a containerized API traffic logging proxy that captures full Claude Code ↔ Anthropic API traffic and ships logs to Seq via OTLP.

## Problem Statement

Claude Code's built-in OTel logging only captures high-level metrics and user prompts. It does not log:
- Full request payloads sent to Anthropic API
- Complete response bodies from Claude
- System prompts and conversation context
- Detailed tool invocation parameters

This limits debugging and auditing capabilities.

## Solution

Bundle `claude-code-logger` (HTTP proxy) and Fluent Bit (OTLP shipper) in a container, integrated with the existing `--otel` sandbox workflow.

See [ADR-005](../../adrs/005-claude-api-traffic-logging.md) for full architecture and rationale.

## Implementation

### Files to Create

| File | Purpose |
|------|---------|
| `scripts/claude-api-logger/Dockerfile` | Container image with claude-code-logger + Fluent Bit |
| `scripts/claude-api-logger/fluent-bit.conf` | OTLP export configuration |
| `scripts/claude-api-logger/entrypoint.sh` | Process supervisor script |

### Files to Modify

| File | Changes |
|------|---------|
| `scripts/claude-sandbox.sh` | Add `--log-api` flag, start logger container, set `ANTHROPIC_BASE_URL` |
| `docs/claude-sandbox.md` | Document `--log-api` usage |

### Container Image

```dockerfile
FROM node:22-slim

RUN apt-get update && apt-get install -y curl gnupg \
    && curl -fsSL https://packages.fluentbit.io/fluentbit.key | gpg --dearmor -o /usr/share/keyrings/fluentbit.gpg \
    && echo "deb [signed-by=/usr/share/keyrings/fluentbit.gpg] https://packages.fluentbit.io/debian/bookworm bookworm main" \
      > /etc/apt/sources.list.d/fluentbit.list \
    && apt-get update && apt-get install -y fluent-bit \
    && rm -rf /var/lib/apt/lists/*

RUN npm install -g claude-code-logger

COPY fluent-bit.conf /etc/fluent-bit/
COPY entrypoint.sh /
RUN chmod +x /entrypoint.sh

EXPOSE 8000
ENTRYPOINT ["/entrypoint.sh"]
```

### Fluent Bit Configuration

```ini
[SERVICE]
    flush        5
    log_level    info

[INPUT]
    name         tail
    path         /var/log/claude-logger.log
    tag          claude.api
    refresh_interval 1

[OUTPUT]
    name                 opentelemetry
    match                *
    host                 ${SEQ_HOST}
    port                 5341
    logs_uri             /ingest/otlp/v1/logs
    tls                  off
```

### Sandbox Integration

```bash
# Usage
./scripts/claude-sandbox.sh --otel --log-api mount

# Starts:
# 1. Seq container (existing --otel behavior)
# 2. claude-api-logger container (new)
# 3. Claude container with ANTHROPIC_BASE_URL=http://api-logger:8000
```

## Acceptance Criteria

- [ ] `--log-api` flag recognized by sandbox script
- [ ] Logger container starts when `--log-api` is used
- [ ] `--log-api` requires `--otel` (Seq must be running)
- [ ] Full API request/response bodies visible in Seq
- [ ] Claude Code functions normally through proxy
- [ ] Streaming responses work correctly
- [ ] No logger container starts without `--log-api`
- [ ] Documentation updated

## Testing

1. Build logger image: `podman build -t claude-api-logger scripts/claude-api-logger/`
2. Run sandbox: `./scripts/claude-sandbox.sh --otel --log-api mount`
3. Verify Seq Dashboard shows API traffic logs
4. Verify Claude Code works normally (try tool calls, long responses)
5. Verify streaming responses are not delayed
6. Run without `--log-api` and verify no logger container starts

## Dependencies

- `--otel` infrastructure (Seq container) - already implemented
- Network connectivity between containers

## Related

- [ADR-005](../../adrs/005-claude-api-traffic-logging.md) - Architecture decision
- `06-ci-cd/006-claude-otel-logging.md` - Basic OTel logging (prerequisite)
- [claude-code-logger](https://github.com/dreampulse/claude-code-logger) - Upstream project
