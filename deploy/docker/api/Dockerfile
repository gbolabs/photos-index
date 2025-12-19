# Multi-stage Dockerfile for .NET API

# Development stage - includes SDK for hot reload
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS development
WORKDIR /app

# Copy Directory.Build.props for centralized package versions
COPY Directory.Build.props ./

# Copy solution and project files
COPY src/PhotosIndex.sln ./src/
COPY src/Api/Api.csproj ./src/Api/
COPY src/Database/Database.csproj ./src/Database/
COPY src/Shared/Shared.csproj ./src/Shared/

# Restore dependencies
RUN dotnet restore ./src/Api/Api.csproj

# Copy source code
COPY src/Api/ ./src/Api/
COPY src/Database/ ./src/Database/
COPY src/Shared/ ./src/Shared/

WORKDIR /app/src/Api
EXPOSE 8080

# Default command for development (can be overridden in docker-compose.override.yml)
CMD ["dotnet", "watch", "run", "--no-launch-profile"]

# Build stage - compile the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy Directory.Build.props for centralized package versions
COPY Directory.Build.props ./

# Copy solution and project files
COPY src/PhotosIndex.sln ./
COPY src/Api/Api.csproj ./Api/
COPY src/Database/Database.csproj ./Database/
COPY src/Shared/Shared.csproj ./Shared/

# Restore dependencies
RUN dotnet restore ./Api/Api.csproj

# Copy source code
COPY src/Api/ ./Api/
COPY src/Database/ ./Database/
COPY src/Shared/ ./Shared/

# Build the application
WORKDIR /src/Api
RUN dotnet build Api.csproj -c Release -o /app/build

# Publish stage - create optimized runtime image
FROM build AS publish
RUN dotnet publish Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage - minimal image for production
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published application
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Api.dll"]
