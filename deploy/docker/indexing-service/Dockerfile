# Multi-stage Dockerfile for IndexingService

# Development stage - includes SDK for hot reload
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS development
WORKDIR /app

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
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

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

# Build the application
WORKDIR /src/IndexingService
RUN dotnet build IndexingService.csproj -c Release -o /app/build

# Publish stage - create optimized runtime image
FROM build AS publish
RUN dotnet publish IndexingService.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage - minimal image for production
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install required libraries for image processing
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    libgdiplus \
    libc6-dev && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

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
