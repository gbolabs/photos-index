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

## Getting Your API Keys

### Anthropic API Key (ANTHROPIC_API_KEY)

The API key authenticates Claude Code with Anthropic's API.

1. **Create an Anthropic account** at https://console.anthropic.com/
2. **Navigate to API Keys**: Go to https://console.anthropic.com/settings/keys
3. **Create a new key**: Click "Create Key", give it a name (e.g., "claude-sandbox")
4. **Copy the key**: It starts with `sk-ant-api...` - save it securely, you won't see it again
5. **Add billing**: Claude Code requires a paid API account. Add a payment method at https://console.anthropic.com/settings/billing

```bash
# Set the key in your shell
export ANTHROPIC_API_KEY="sk-ant-api03-..."

# Or add to your shell profile (~/.bashrc, ~/.zshrc)
echo 'export ANTHROPIC_API_KEY="sk-ant-api03-..."' >> ~/.bashrc
```

**Cost considerations**: Claude Code uses the Anthropic API directly. Monitor usage at https://console.anthropic.com/settings/usage

### GitHub Token (GH_TOKEN)

The GitHub token enables `gh` CLI operations (PRs, issues, cloning private repos).

#### Option 1: Use Existing gh CLI Authentication (Recommended)

If you already have `gh` CLI authenticated:

```bash
# Get token from existing gh auth
export GH_TOKEN=$(gh auth token)
```

#### Option 2: Create a Personal Access Token (PAT)

1. **Go to GitHub Settings**: https://github.com/settings/tokens?type=beta
2. **Generate new token**: Click "Generate new token" (Fine-grained token recommended)
3. **Configure the token**:
   - **Name**: `claude-sandbox`
   - **Expiration**: Choose based on your security preferences
   - **Repository access**: Select repositories Claude should access
   - **Permissions** (minimum required):
     - `Contents`: Read and write (for commits, branches)
     - `Pull requests`: Read and write (for creating PRs)
     - `Issues`: Read and write (optional, for issue management)
     - `Metadata`: Read (required)
4. **Generate and copy**: Save the token securely

```bash
# Set the token
export GH_TOKEN="github_pat_..."

# Or add to your shell profile
echo 'export GH_TOKEN="github_pat_..."' >> ~/.bashrc
```

#### Option 3: Classic Personal Access Token

For simpler setup (less granular permissions):

1. Go to https://github.com/settings/tokens
2. Click "Generate new token (classic)"
3. Select scopes: `repo`, `workflow` (if needed)
4. Generate and copy the token

### Verifying Your Setup

```bash
# Verify Anthropic API key works
curl -s https://api.anthropic.com/v1/messages \
  -H "x-api-key: $ANTHROPIC_API_KEY" \
  -H "anthropic-version: 2023-06-01" \
  -H "content-type: application/json" \
  -d '{"model":"claude-sonnet-4-20250514","max_tokens":10,"messages":[{"role":"user","content":"Hi"}]}' \
  | jq -r '.content[0].text // .error.message'

# Verify GitHub token works
gh auth status
# Or: curl -s -H "Authorization: Bearer $GH_TOKEN" https://api.github.com/user | jq .login
```

### Security Best Practices

1. **Never commit tokens**: Add to `.gitignore` or use environment variables
2. **Use token expiration**: Set reasonable expiration dates for GitHub PATs
3. **Minimal permissions**: Only grant permissions actually needed
4. **Rotate regularly**: Regenerate tokens periodically
5. **Use secrets manager**: Consider using `pass`, `1password-cli`, or similar:
   ```bash
   export ANTHROPIC_API_KEY=$(pass show anthropic/api-key)
   export GH_TOKEN=$(pass show github/claude-sandbox-token)
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

## Observability (OTel)

Enable OpenTelemetry logging to capture prompts and tool calls in Seq.

### Usage

```bash
# Start sandbox with OTel enabled
./scripts/claude-sandbox.sh --otel mount

# View prompts and tool calls
open http://localhost:5341
```

### What Gets Logged

- All user prompts sent to Claude
- Tool calls (Bash, Read, Write, etc.)
- Response metadata
- Timestamps and session IDs

### Architecture

The `--otel` flag:
1. Starts a Seq container (port 5341)
2. Sets OTel environment variables for Claude Code
3. All telemetry flows to Seq via OTLP

```
┌─────────────────────────────────────────────────────────┐
│ Host Machine                                            │
│                                                         │
│  ┌──────────────────┐     ┌──────────────────────────┐ │
│  │ Seq Dashboard    │◄────│ Claude Code Container    │ │
│  │ :5341 (UI+OTLP)  │     │                          │ │
│  │                  │     │ OTEL_EXPORTER_OTLP_*     │ │
│  └──────────────────┘     │ CLAUDE_CODE_ENABLE_*     │ │
│                           │ OTEL_LOG_USER_PROMPTS=1  │ │
│                           └──────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### Stopping

The Seq container runs with `--rm` and stops when the sandbox exits.
To stop manually: `podman rm -f seq-otel`

## Full API Traffic Logging

For complete visibility into all API requests and responses between Claude Code and Anthropic, use the `--log-api` flag.

### Usage

```bash
# Enable full API traffic logging (requires --otel)
./scripts/claude-sandbox.sh --otel --log-api mount

# View in Seq Dashboard
open http://localhost:5341
```

### What Gets Logged

With `--log-api`, you get **everything**:

| Data | `--otel` only | `--otel --log-api` |
|------|---------------|---------------------|
| User prompts | ✅ | ✅ |
| Tool calls | ✅ | ✅ |
| Full request bodies | ❌ | ✅ |
| Full response bodies | ❌ | ✅ |
| System prompts | ❌ | ✅ |
| Conversation context | ❌ | ✅ |

### Architecture

The `--log-api` flag adds an HTTP proxy between Claude Code and Anthropic:

```
┌──────────────────────────────────────────────────────────────────┐
│ Host / Podman Network                                            │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ claude-api-logger Container                                 │ │
│  │                                                             │ │
│  │  ┌─────────────────────┐     ┌────────────────────────┐    │ │
│  │  │ claude-code-logger  │────►│ Fluent Bit             │    │ │
│  │  │ :8000 (HTTP)        │ log │ (OTLP export)          │────┼─┼──► Seq :5341
│  │  └─────────────────────┘     └────────────────────────┘    │ │
│  │           ▲                                                 │ │
│  └───────────┼─────────────────────────────────────────────────┘ │
│              │ HTTP (internal)                                   │
│              │                                                   │
│  ┌───────────┴─────────────────┐                                │
│  │ Claude Code Container       │                                │
│  │ ANTHROPIC_BASE_URL=         │──────────────────────────────► │
│  │   http://api-logger:8800    │           HTTPS               │
│  └─────────────────────────────┘      api.anthropic.com        │
└──────────────────────────────────────────────────────────────────┘
```

### How It Works

1. **HTTP Bridge**: Claude Code connects to the proxy via HTTP (no TLS complexity)
2. **Proxy captures traffic**: Full request/response bodies are logged
3. **Fluent Bit ships logs**: Logs are sent to Seq via OTLP
4. **Proxy forwards to Anthropic**: Requests continue to `api.anthropic.com` via HTTPS

### Performance Impact

The proxy adds negligible latency (~1-5ms) on API calls that typically take 2-30+ seconds.

### Security Notes

- HTTP traffic is **internal only** (within Podman network)
- API key is visible in logs - treat Seq data as sensitive
- No traffic leaves the container unencrypted (proxy uses HTTPS to Anthropic)

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
