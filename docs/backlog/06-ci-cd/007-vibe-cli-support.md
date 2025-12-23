# 007: Mistral Vibe CLI Support

**Status**: ✅ Complete
**PR**: [#91](https://github.com/gbolabs/photos-index/pull/91)
**Priority**: P3
**Effort**: 30 minutes

## Objective

Add support for Mistral Vibe CLI with project-specific context, enabling developers to use Devstral-2 as an alternative AI coding assistant.

## Problem Statement

The project already supports:
- Claude Code via `CLAUDE.md`
- GitHub Copilot via `.github/copilot-instructions.md`

Mistral Vibe CLI is a new open-source coding assistant but lacks a project-level instruction file feature. Project context must be installed to the user's home directory.

## Solution

Create a Vibe configuration that:
1. Stores project instructions in `.vibe/prompts/photos-index.md`
2. Configures `.vibe/config.toml` to reference the prompt
3. Extends `scripts/setup-dev-env.sh` to copy the prompt to `~/.vibe/prompts/`

## Implementation

### Files Created

| File | Purpose |
|------|---------|
| `.vibe/prompts/photos-index.md` | Project instructions for Vibe |
| `.vibe/config.toml` | Project configuration (references prompt) |

### Files Modified

| File | Change |
|------|--------|
| `scripts/setup-dev-env.sh` | Added `setup_vibe()` function |

## Usage

After running the setup script, developers can use Vibe with project context:

```bash
# Run setup (copies prompt to ~/.vibe/prompts/)
./scripts/setup-dev-env.sh

# Use Vibe in the project
cd /path/to/photos-index
vibe
```

## Acceptance Criteria

- [x] `.vibe/prompts/photos-index.md` contains project instructions
- [x] `.vibe/config.toml` references the prompt
- [x] `setup-dev-env.sh` copies prompt to `~/.vibe/prompts/`
- [x] Documentation updated

## Related

- `CLAUDE.md` - Claude Code instructions
- `.github/copilot-instructions.md` - GitHub Copilot instructions
- [Mistral Vibe CLI](https://github.com/mistralai/mistral-vibe)
