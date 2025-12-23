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
echo ""
echo "💡 VS Code: Run 'code-server --bind-addr 0.0.0.0:8443' to access via browser"
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
