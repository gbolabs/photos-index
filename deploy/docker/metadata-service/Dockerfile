# Multi-stage Dockerfile for MetadataService

# Build arguments for version info
ARG BUILD_VERSION=0.0.0-dev
ARG BUILD_COMMIT_HASH=dev
ARG BUILD_BRANCH=local
ARG BUILD_TIME

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
COPY src/MetadataService/MetadataService.csproj ./MetadataService/
COPY src/Shared/Shared.csproj ./Shared/

# Restore dependencies
RUN dotnet restore ./MetadataService/MetadataService.csproj

# Copy source code
COPY src/MetadataService/ ./MetadataService/
COPY src/Shared/ ./Shared/

# Build the application with version
WORKDIR /src/MetadataService
RUN dotnet build MetadataService.csproj -c Release -o /app/build -p:Version=${BUILD_VERSION}

# Publish stage - create optimized runtime image
FROM build AS publish
ARG BUILD_VERSION=0.0.0-dev
RUN dotnet publish MetadataService.csproj -c Release -o /app/publish /p:UseAppHost=false -p:Version=${BUILD_VERSION}

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
# libheif and libde265 for HEIC decoding, x265 for HEVC
RUN apk add --no-cache \
    libgdiplus \
    libheif \
    libheif-dev \
    libde265 \
    x265-libs \
    && ln -sf /usr/lib/libheif.so.1 /usr/lib/libheif.so || true

# Create non-root user
RUN addgroup -S appuser && adduser -S appuser -G appuser

# Copy published application
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Entry point
ENTRYPOINT ["dotnet", "MetadataService.dll"]
