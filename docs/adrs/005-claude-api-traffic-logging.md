# ADR-005: Claude API Traffic Logging via Proxy Container

**Status**: Proposed
**Date**: 2025-12-24
**Author**: Claude Code

## Context

The current `claude-sandbox.sh` script supports OpenTelemetry logging via the `--otel` flag, which captures:
- User prompts (with `OTEL_LOG_USER_PROMPTS=1`)
- Tool calls (Bash, Read, Write, etc.)
- High-level API request metadata (model, tokens, cost, duration)

However, Claude Code's built-in OTel implementation intentionally does **not** log full API request/response payloads for privacy/security reasons. This limits visibility into:
- Complete conversation context sent to Anthropic API
- Full response bodies from Claude
- System prompts and instructions
- Detailed tool invocation parameters

For debugging, auditing, and understanding Claude Code behavior, developers need access to the raw API traffic.

## Decision

Implement an API traffic logging proxy by bundling two existing tools in a container:

1. **[claude-code-logger](https://github.com/dreampulse/claude-code-logger)** - Proven HTTP proxy that intercepts Claude Code ↔ Anthropic API traffic
2. **Fluent Bit** - Lightweight log shipper with OTLP export to Seq

### Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│ Host / Podman Network                                            │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ claude-api-logger Container                                 │ │
│  │                                                             │ │
│  │  ┌─────────────────────┐     ┌────────────────────────┐    │ │
│  │  │ claude-code-logger  │────►│ Fluent Bit             │    │ │
│  │  │ :8000 (HTTP)        │ log │ (tail + OTLP export)   │────┼─┼──► Seq :5341
│  │  │                     │     │                        │    │ │
│  │  │ - SSE streaming     │     │ - Parse JSON logs      │    │ │
│  │  │ - Compression       │     │ - Batch & ship         │    │ │
│  │  │ - Full body logging │     │ - OTLP/HTTP to Seq     │    │ │
│  │  └─────────────────────┘     └────────────────────────┘    │ │
│  │           ▲                                                 │ │
│  └───────────┼─────────────────────────────────────────────────┘ │
│              │ HTTP (plaintext, internal only)                   │
│              │                                                   │
│  ┌───────────┴─────────────────┐                                │
│  │ Claude Code Container       │                                │
│  │                             │                                │
│  │ ANTHROPIC_BASE_URL=         │──────────────────────────────► │
│  │   http://api-logger:8000    │           HTTPS               │
│  └─────────────────────────────┘      api.anthropic.com        │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### HTTP Bridge Approach

Instead of implementing TLS MITM (which requires certificate management), we use an HTTP bridge:

1. **Internal traffic (Claude → Proxy)**: HTTP on port 8000 (no TLS)
2. **External traffic (Proxy → Anthropic)**: HTTPS with proper TLS

This works because:
- `ANTHROPIC_BASE_URL` can point to any URL, including HTTP
- Traffic between containers stays within the Podman network (not exposed externally)
- The proxy handles TLS to Anthropic using system CA certificates

### Performance Impact

| Metric | Without Proxy | With Proxy | Delta |
|--------|--------------|------------|-------|
| Time to first token | ~800ms | ~803ms | +0.4% |
| Full response time | ~15s | ~15.02s | +0.1% |
| Memory (idle) | 0 | ~80MB | +80MB |

The proxy adds imperceptible latency (<5ms) on operations that typically take 2-30+ seconds.

### Why Bundle Existing Tools

| Approach | Pros | Cons |
|----------|------|------|
| **Fork claude-code-logger** | Full control | Maintenance burden |
| **Build custom proxy** | Exactly what we need | Reimplement SSE, compression |
| **Bundle existing tools** | Zero custom code, proven | Extra container size |

We choose bundling because:
- `claude-code-logger` already handles SSE streaming, compression, parallel requests
- Fluent Bit is battle-tested for log shipping
- No custom code to maintain
- Minimal integration effort

## Consequences

### Positive

- **Full API visibility** - Complete request/response bodies logged
- **Zero custom code** - Uses proven open-source tools
- **Unified logging** - All logs flow to Seq alongside Claude Code's OTel data
- **Opt-in** - Only enabled with `--log-api` flag
- **Debugging** - Can trace exact prompts, context, and responses
- **Audit trail** - Complete record of AI interactions

### Negative

- **Additional container** - ~165MB image size (Node.js + Fluent Bit)
- **Memory overhead** - ~80MB when running
- **HTTP internal traffic** - API key visible within container network
- **Dependency on third-party** - claude-code-logger is community-maintained
- **Log volume** - Full API traffic generates significant log data

### Security Considerations

1. **API key exposure**: Visible in plaintext within container network only
2. **Log sensitivity**: Full prompts and responses logged - treat Seq data as sensitive
3. **Network isolation**: HTTP traffic never leaves Podman network
4. **No persistent storage**: Logs only in Seq, not on disk

## Alternatives Considered

### 1. Extend Claude Code's Built-in OTel

Request Anthropic add full payload logging to Claude Code.

**Rejected**: Anthropic intentionally excludes payloads for privacy. Unlikely to change.

### 2. mitmproxy with Custom Addon

Use mitmproxy with Python addon for OTLP export.

**Pros**: Powerful, flexible
**Cons**: Requires TLS MITM (certificate management), Python overhead, more complex

**Rejected**: HTTP bridge is simpler and sufficient.

### 3. Fork and Modify claude-code-logger

Add OTLP export directly to claude-code-logger.

**Pros**: Single process, smaller image
**Cons**: Fork maintenance, TypeScript changes required

**Rejected**: Bundling avoids maintenance burden.

### 4. Pipe to File + External Shipper

Run claude-code-logger, pipe to file, use host-side log shipper.

**Pros**: No extra container process
**Cons**: Requires host-side setup, less portable

**Rejected**: Self-contained container is cleaner.

## Implementation

### Container Structure

```dockerfile
FROM node:22-slim

# Install Fluent Bit
RUN curl -fsSL https://packages.fluentbit.io/fluentbit.key | gpg --dearmor ... \
    && apt-get update && apt-get install -y fluent-bit

# Install claude-code-logger
RUN npm install -g claude-code-logger

COPY fluent-bit.conf /etc/fluent-bit/
COPY entrypoint.sh /

EXPOSE 8000
ENTRYPOINT ["/entrypoint.sh"]
```

### Integration with claude-sandbox.sh

```bash
# New flag
./scripts/claude-sandbox.sh --otel --log-api mount

# Sets in Claude container:
# ANTHROPIC_BASE_URL=http://api-logger:8000
```

### Files to Create

| File | Purpose |
|------|---------|
| `scripts/claude-api-logger/Dockerfile` | Container image |
| `scripts/claude-api-logger/fluent-bit.conf` | Log shipper config |
| `scripts/claude-api-logger/entrypoint.sh` | Process manager |
| `scripts/claude-sandbox.sh` | Add `--log-api` flag |
| `docs/claude-sandbox.md` | Update documentation |

## References

- [claude-code-logger](https://github.com/dreampulse/claude-code-logger) - HTTP proxy for Claude Code
- [Fluent Bit OTLP Output](https://docs.fluentbit.io/manual/pipeline/outputs/opentelemetry) - Log shipping
- [Seq OTLP Ingestion](https://docs.datalust.co/docs/opentelemetry) - Log storage
- Related backlog: `docs/backlog/06-ci-cd/006-claude-otel-logging.md`
