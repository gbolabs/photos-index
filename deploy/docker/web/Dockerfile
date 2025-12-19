# Multi-stage Dockerfile for Angular Web Application

# Development stage - for hot reload
FROM node:24-alpine AS development
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
FROM node:24-alpine AS build
WORKDIR /app

# Copy package files
COPY src/Web/package*.json ./

# Install dependencies (including dev dependencies for build)
RUN npm ci

# Copy source code
COPY src/Web/ ./

# Build Angular application for production
RUN npm run build -- --configuration production

# Runtime stage - nginx to serve static files
FROM nginx:1.27-alpine AS runtime

# Install curl for healthcheck
RUN apk add --no-cache curl

# Remove default nginx configuration
RUN rm -rf /etc/nginx/conf.d/*

# Copy custom nginx configuration
COPY deploy/docker/web/nginx.conf /etc/nginx/conf.d/default.conf

# Copy built Angular application from build stage
COPY --from=build /app/dist/web/browser /usr/share/nginx/html

# Copy environment configuration script
COPY deploy/docker/web/env.sh /docker-entrypoint.d/40-env.sh
RUN chmod +x /docker-entrypoint.d/40-env.sh

# Create non-root user (nginx already exists in the image)
RUN chown -R nginx:nginx /usr/share/nginx/html && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx && \
    chown -R nginx:nginx /etc/nginx/conf.d

# Switch to non-root user
USER nginx

EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:80/ || exit 1

# Nginx will start automatically via the base image's entrypoint
