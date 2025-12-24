# Mistral API Key Support

## Overview
The Vibe sandbox now supports automatic forwarding of the Mistral API key via environment variable.

## How It Works

The `vibe-sandbox.sh` script automatically forwards the `MISTRAL_API_KEY` environment variable from your host machine to the container. The Vibe CLI (mistral-vibe) automatically detects and uses this environment variable for authentication.

## Usage

### Method 1: Export Before Running (Recommended)
```bash
# Set your Mistral API key
export MISTRAL_API_KEY="your-mistral-api-key-here"

# Run the Vibe sandbox
./scripts/vibe-sandbox.sh clone
```

### Method 2: Inline (One-time use)
```bash
MISTRAL_API_KEY="your-mistral-api-key-here" ./scripts/vibe-sandbox.sh clone
```

### Method 3: In Shell Configuration (Permanent)
```bash
# Add to ~/.bashrc, ~/.zshrc, or ~/.profile
export MISTRAL_API_KEY="your-mistral-api-key-here"

# Then source the file or restart your shell
source ~/.bashrc
```

### Method 4: Using .env File
```bash
# Create a .env file in your project root
echo "MISTRAL_API_KEY=your-mistral-api-key-here" > .env

# Load it before running
source .env
./scripts/vibe-sandbox.sh clone
```

## Additional Environment Variables

The Vibe CLI also supports these environment variables:

- **`VIBE_MODEL`**: Default model to use (e.g., "mistral-small-latest")
- **`MISTRAL_BASE_URL`**: Custom API endpoint URL
- **`MISTRAL_TIMEOUT`**: Request timeout in seconds

Example:
```bash
export MISTRAL_API_KEY="your-key"
export VIBE_MODEL="mistral-small-latest"
./scripts/vibe-sandbox.sh clone
```

## Security Considerations

1. **Never commit API keys to version control**
2. **Use .gitignore** to exclude files containing API keys
3. **Be cautious with container logs** - API keys might appear in logs
4. **Use temporary containers** - The sandbox uses `--rm` to clean up after exit
5. **Consider using secrets management** for production environments

## Troubleshooting

### Vibe CLI doesn't recognize my API key
- Verify the variable is set: `echo $MISTRAL_API_KEY`
- Check it's being passed to the container: `./scripts/vibe-sandbox.sh clone 2>&1 | grep MISTRAL_API_KEY`
- Verify the container has it: `podman exec -it vibe-sandbox env | grep MISTRAL_API_KEY`

### Where to get a Mistral API key
Visit [https://mistral.ai](https://mistral.ai) and sign up for an account to get your API key.

## Implementation Details

The implementation adds `-e MISTRAL_API_KEY="${MISTRAL_API_KEY:-}"` to both `run_mount_mode()` and `run_clone_mode()` functions in `vibe-sandbox.sh`. This ensures the API key is automatically forwarded to the container if it's set on the host machine.

