# Tag Creation

This directory contains tools for creating and managing version tags for releases.

## Quick Start

### Using the Script (Local)

```bash
# Create a new tag
./scripts/create-tag.sh 1.0.0

# Create a tag with a custom message
./scripts/create-tag.sh 1.0.0 -m "Release version 1.0.0 with new features"

# Dry run (see what would happen without creating the tag)
./scripts/create-tag.sh 1.0.0 --dry-run

# Force overwrite an existing tag
./scripts/create-tag.sh 1.0.0 --force
```

### Using GitHub Actions (Remote)

1. Go to **Actions** → **Create Tag**
2. Click **Run workflow**
3. Fill in the version number (e.g., `1.0.0` or `v1.0.0`)
4. Optionally add a custom message
5. Check **force** if you need to overwrite an existing tag
6. Click **Run workflow**

## Workflow

1. **Create Tag**: Use either the script or GitHub Actions to create a tag
2. **Automatic Release**: The [release.yml](.github/workflows/release.yml) workflow automatically triggers when a tag is pushed
3. **Build & Deploy**: Docker images are built and pushed to GitHub Container Registry
4. **GitHub Release**: A GitHub release is created with changelog

## Version Format

Tags must follow semantic versioning: `v<major>.<minor>.<patch>`

Examples:
- ✓ `v1.0.0`
- ✓ `v2.1.3`
- ✓ `v0.3.0-beta.1`
- ✗ `1.0` (missing patch version)
- ✗ `v1.0.0.0` (too many version parts)

## Files

- [scripts/create-tag.sh](scripts/create-tag.sh) - Script to create and push tags locally
- [.github/workflows/create-tag.yml](.github/workflows/create-tag.yml) - GitHub Actions workflow for remote tag creation
- [.github/workflows/release.yml](.github/workflows/release.yml) - Automatic release workflow triggered by tags

## Safety Features

- Validates semantic version format
- Checks for uncommitted changes (script only)
- Prevents accidental overwrites (use `--force` to override)
- Dry-run mode available (script only)
- Requires confirmation before pushing (script only)

## Examples

### Creating a Major Release
```bash
./scripts/create-tag.sh 2.0.0 -m "Major release with breaking changes"
```

### Creating a Minor Release
```bash
./scripts/create-tag.sh 1.5.0 -m "New features added"
```

### Creating a Patch Release
```bash
./scripts/create-tag.sh 1.0.1 -m "Bug fixes"
```

### Creating a Pre-release
```bash
./scripts/create-tag.sh 1.0.0-beta.1 -m "Beta release for testing"
```
