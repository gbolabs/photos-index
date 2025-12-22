# Multi-stage Dockerfile for IndexingService

# Build arguments for version info
ARG BUILD_VERSION=0.0.0-dev
ARG BUILD_COMMIT_HASH=dev
ARG BUILD_BRANCH=local
ARG BUILD_TIME

# Development stage - includes SDK for hot reload
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS development
WORKDIR /app

# Copy Directory.Build.props and Directory.Packages.props for centralized package versions
COPY Directory.Build.props ./
COPY Directory.Packages.props ./

# Copy solution and project files
COPY src/PhotosIndex.sln ./src/
COPY src/IndexingService/IndexingService.csproj ./src/IndexingService/
COPY src/Database/Database.csproj ./src/Database/
COPY src/Shared/Shared.csproj ./src/Shared/

# Restore dependencies
RUN dotnet restore ./src/IndexingService/IndexingService.csproj

# Copy source code
COPY src/IndexingService/ ./src/IndexingService/
COPY src/Database/ ./src/Database/
COPY src/Shared/ ./src/Shared/

WORKDIR /app/src/IndexingService

# Default command for development (can be overridden in docker-compose.override.yml)
CMD ["dotnet", "watch", "run", "--no-launch-profile"]

# Build stage - compile the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Re-declare build arg for this stage
ARG BUILD_VERSION=0.0.0-dev

WORKDIR /src

# Copy Directory.Build.props and Directory.Packages.props for centralized package versions
COPY Directory.Build.props ./
COPY Directory.Packages.props ./

# Copy solution and project files
COPY src/PhotosIndex.sln ./
COPY src/IndexingService/IndexingService.csproj ./IndexingService/
COPY src/Database/Database.csproj ./Database/
COPY src/Shared/Shared.csproj ./Shared/

# Restore dependencies
RUN dotnet restore ./IndexingService/IndexingService.csproj

# Copy source code
COPY src/IndexingService/ ./IndexingService/
COPY src/Database/ ./Database/
COPY src/Shared/ ./Shared/

# Build the application with version
WORKDIR /src/IndexingService
RUN dotnet build IndexingService.csproj -c Release -o /app/build -p:Version=${BUILD_VERSION}

# Publish stage - create optimized runtime image
FROM build AS publish
ARG BUILD_VERSION=0.0.0-dev
RUN dotnet publish IndexingService.csproj -c Release -o /app/publish /p:UseAppHost=false -p:Version=${BUILD_VERSION}

# Runtime stage - minimal image for production
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Re-declare build args for this stage
ARG BUILD_VERSION=0.0.0-dev
ARG BUILD_COMMIT_HASH=dev
ARG BUILD_BRANCH=local
ARG BUILD_TIME

# Set as environment variables
ENV BUILD_VERSION=${BUILD_VERSION}
ENV BUILD_COMMIT_HASH=${BUILD_COMMIT_HASH}
ENV BUILD_BRANCH=${BUILD_BRANCH}
ENV BUILD_TIME=${BUILD_TIME}

WORKDIR /app

# Install required libraries for image processing (including HEIC/HEIF support)
# Alpine uses musl libc, libheif is available in community repo
RUN apk add --no-cache \
    libgdiplus \
    libheif

# Create non-root user
RUN addgroup -S appuser && adduser -S appuser -G appuser

# Create directories for thumbnails and temp files
RUN mkdir -p /app/thumbnails /app/temp && \
    chown -R appuser:appuser /app

# Copy published application
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Entry point
ENTRYPOINT ["dotnet", "IndexingService.dll"]
