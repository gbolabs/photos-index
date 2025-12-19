#!/bin/sh

# This script runs before nginx starts and injects environment variables
# into the Angular application's runtime configuration

set -e

# Create env-config.js with runtime environment variables
cat <<EOF > /usr/share/nginx/html/assets/env-config.js
window.__env = window.__env || {};
window.__env.apiUrl = '${API_URL:-http://localhost:5000}';
window.__env.production = ${PRODUCTION:-true};
EOF

echo "Environment configuration created:"
cat /usr/share/nginx/html/assets/env-config.js
