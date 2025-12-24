# Implementation Checklist: Move uv/vibe Installation to Init Script

## Problem
The installation of `uv` and `vibe` CLI was failing in the Docker container because:
1. Tools were being installed as root but the container runs as a non-root user (`vibe`)
2. PATH issues prevented the non-root user from accessing installed binaries
3. Using `--system-path` required root privileges

## Solution
Moved the installation of `uv` and `vibe` from the Dockerfile to an init script that runs as the user, avoiding permission issues.

## Changes Made

### 1. Created `init-vibe.sh`
- New initialization script that runs as the `vibe` user
- Installs `uv` in the user's home directory (`~/.local/bin`)
- Installs `vibe` CLI using `uv tool install` in user space (`~/.local/share/uv/tools`)
- Updates `.bashrc` to include the installation directories in PATH
- Handles both fresh installations and re-runs (idempotent)

### 2. Updated `Dockerfile.vibe-sandbox`
- **Removed**: All uv and vibe installation steps from the Dockerfile
- **Added**: Copy of `init-vibe.sh` to the container
- **Kept**: Python and other prerequisites in the Dockerfile
- **Kept**: User creation and workspace setup

### 3. Updated `entrypoint.vibe.sh`
- **Added**: Call to `/init-vibe.sh` at the beginning
- **Added**: Source of `~/.bashrc` to ensure PATH includes uv and vibe binaries
- **Improved**: Error handling and user feedback

## Benefits

1. **Permission Issues Resolved**: Installation runs as the user who will use the tools
2. **Cleaner Dockerfile**: No need for complex PATH manipulations or root installations
3. **User-Space Installation**: Tools are installed in the user's home directory, making them portable
4. **Idempotent**: Script can be re-run without errors
5. **Better Error Handling**: Clear error messages if installation fails
6. **Maintainability**: Easier to update uv/vibe versions by modifying the init script

## Testing

To test the changes:

1. Build the container:
   ```bash
   ./scripts/vibe-sandbox.sh build
   ```

2. Run in clone mode:
   ```bash
   ./scripts/vibe-sandbox.sh clone
   ```

3. Run in mount mode:
   ```bash
   ./scripts/vibe-sandbox.sh mount
   ```

4. Verify vibe is installed and working:
   ```bash
   vibe --version
   ```

## Rollback Plan

If issues arise, the previous Dockerfile can be restored by:
1. Reverting the Dockerfile changes
2. Removing the init script
3. Updating the entrypoint to not call the init script

## Notes

- The init script runs on every container startup, but checks if tools are already installed
- uv and vibe are installed in user space, so they persist across container runs
- The `.bashrc` is updated to ensure PATH is correct for interactive shells
