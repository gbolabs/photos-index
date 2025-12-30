#!/bin/sh

# This script runs before nginx starts and injects environment variables
# into the Angular application's runtime configuration

set -e

# Set default values
export NGINX_PORT=${NGINX_PORT:-80}
export API_URL=${API_URL:-http://localhost:5000}
export PRODUCTION=${PRODUCTION:-true}

# Substitute environment variables in nginx config
envsubst '${NGINX_PORT}' < /etc/nginx/conf.d/default.conf > /etc/nginx/conf.d/default.conf.tmp
mv /etc/nginx/conf.d/default.conf.tmp /etc/nginx/conf.d/default.conf

echo "Nginx configured to listen on port: ${NGINX_PORT}"

# Get version from environment (set at build time or runtime)
export APP_VERSION=${APP_VERSION:-unknown}

# Create env-config.js with runtime environment variables
cat <<EOF > /usr/share/nginx/html/assets/env-config.js
window.__env = window.__env || {};
window.__env.apiUrl = '${API_URL}';
window.__env.production = ${PRODUCTION};
window.__env.version = '${APP_VERSION}';
EOF

echo "Environment configuration created:"
cat /usr/share/nginx/html/assets/env-config.js
