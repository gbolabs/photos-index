#!/bin/bash
# Test script to verify uv and vibe installation

set -e

echo "Testing uv and vibe installation..."
echo ""

# Test 1: Check if init-vibe.sh exists
if [ -f "init-vibe.sh" ]; then
    echo "✅ init-vibe.sh exists"
else
    echo "❌ init-vibe.sh not found"
    exit 1
fi

# Test 2: Check if init-vibe.sh is executable
if [ -x "init-vibe.sh" ]; then
    echo "✅ init-vibe.sh is executable"
else
    echo "❌ init-vibe.sh is not executable"
    exit 1
fi

# Test 3: Check if entrypoint.vibe.sh exists
if [ -f "entrypoint.vibe.sh" ]; then
    echo "✅ entrypoint.vibe.sh exists"
else
    echo "❌ entrypoint.vibe.sh not found"
    exit 1
fi

# Test 4: Check if entrypoint.vibe.sh is executable
if [ -x "entrypoint.vibe.sh" ]; then
    echo "✅ entrypoint.vibe.sh is executable"
else
    echo "❌ entrypoint.vibe.sh is not executable"
    exit 1
fi

# Test 5: Check if Dockerfile.vibe-sandbox exists
if [ -f "Dockerfile.vibe-sandbox" ]; then
    echo "✅ Dockerfile.vibe-sandbox exists"
else
    echo "❌ Dockerfile.vibe-sandbox not found"
    exit 1
fi

# Test 6: Verify Dockerfile doesn't contain uv installation as root
if grep -q "uv tool install --system-path" Dockerfile.vibe-sandbox; then
    echo "❌ Dockerfile still contains root installation of uv"
    exit 1
else
    echo "✅ Dockerfile doesn't contain root installation of uv"
fi

# Test 7: Verify Dockerfile copies init-vibe.sh
if grep -q "COPY scripts/init-vibe.sh" Dockerfile.vibe-sandbox; then
    echo "✅ Dockerfile copies init-vibe.sh"
else
    echo "❌ Dockerfile doesn't copy init-vibe.sh"
    exit 1
fi

# Test 8: Verify entrypoint calls init script
if grep -q "/init-vibe.sh" entrypoint.vibe.sh; then
    echo "✅ entrypoint.vibe.sh calls init-vibe.sh"
else
    echo "❌ entrypoint.vibe.sh doesn't call init-vibe.sh"
    exit 1
fi

# Test 9: Verify entrypoint sources .bashrc
if grep -q "source.*\.bashrc" entrypoint.vibe.sh; then
    echo "✅ entrypoint.vibe.sh sources .bashrc"
else
    echo "❌ entrypoint.vibe.sh doesn't source .bashrc"
    exit 1
fi

echo ""
echo "All tests passed! ✅"
echo ""
echo "Summary of changes:"
echo "  - Created init-vibe.sh for user-space installation"
echo "  - Removed root installation from Dockerfile"
echo "  - Updated entrypoint to run init script"
echo "  - Added PATH configuration via .bashrc"
