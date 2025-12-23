#!/bin/bash
echo "🤖 Claude Code Sandbox"
echo "======================"
echo "Mode: YOLO (all permissions granted)"
echo ""
if [[ -n "${GH_TOKEN:-}" ]]; then
    echo "✅ GitHub CLI authenticated"
else
    echo "⚠️  GitHub CLI not authenticated (GH_TOKEN not set)"
fi
if [[ "${CLAUDE_CODE_ENABLE_TELEMETRY:-}" == "1" ]]; then
    echo "✅ Telemetry: Enabled (OTel to ${OTEL_EXPORTER_OTLP_ENDPOINT:-not set})"
else
    echo "ℹ️  Telemetry: Disabled (use --otel flag to enable)"
fi
echo ""
# Start code-server in the background
echo "🚀 Starting code-server on 0.0.0.0:8443..."
code-server --bind-addr 0.0.0.0:8443 --auth none &
echo "✅ code-server running at http://0.0.0.0:8443"
echo ""

# Clone repo if in clone mode
if [[ -n "${REPO_URL:-}" ]]; then
    echo "Cloning repository: $REPO_URL"
    git clone "$REPO_URL" . || { echo "Failed to clone repository"; exit 1; }
    if [[ -n "${BRANCH:-}" ]]; then
        echo "Checking out branch: $BRANCH"
        git checkout "$BRANCH" || { echo "Failed to checkout branch"; exit 1; }
    fi
    echo ""
fi

echo "Starting Claude Code..."
echo ""
exec claude --dangerously-skip-permissions "$@"
