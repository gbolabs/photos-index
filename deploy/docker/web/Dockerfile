# Multi-stage Dockerfile for Angular Web Application

# Development stage - for hot reload
FROM docker.io/library/node:24-alpine AS development
WORKDIR /app

# Copy package files
COPY src/Web/package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY src/Web/ ./

# Expose port for development server
EXPOSE 4200

# Default command for development (can be overridden in docker-compose.override.yml)
CMD ["npm", "start", "--", "--host", "0.0.0.0", "--poll=2000"]

# Build stage - compile Angular application
FROM docker.io/library/node:24-alpine AS build
WORKDIR /app

# Increase Node.js memory limit for Angular build
ENV NODE_OPTIONS="--max-old-space-size=4096"

# Copy package files
COPY src/Web/package*.json ./

# Install dependencies (including dev dependencies for build)
RUN npm ci

# Copy source code
COPY src/Web/ ./

# Build Angular application for production
RUN npm run build -- --configuration production

# Runtime stage - nginx to serve static files (slim variant is ~3x smaller)
FROM docker.io/library/nginx:alpine-slim AS runtime

# Install curl for healthcheck
RUN apk add --no-cache curl

# Remove default nginx configuration
RUN rm -rf /etc/nginx/conf.d/*

# Copy custom nginx configuration
COPY deploy/docker/web/nginx.conf /etc/nginx/conf.d/default.conf

# Copy built Angular application from build stage
# Angular 17+ with application builder outputs to dist/<project>/browser/
COPY --from=build /app/dist/Web/browser /usr/share/nginx/html

# Create assets directory for runtime config
RUN mkdir -p /usr/share/nginx/html/assets

# Copy environment configuration script and set permissions
COPY --chmod=755 deploy/docker/web/env.sh /docker-entrypoint.d/40-env.sh

EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:80/ || exit 1

# Nginx will start automatically via the base image's entrypoint
