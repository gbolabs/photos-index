# Synology NAS Deployment Guide

This guide covers deploying Photos Index on a Synology NAS using Docker.

## Prerequisites

- Synology NAS with DSM 7.0 or later
- Docker package installed from Package Center
- SSH access (recommended for initial setup)
- At least 2GB RAM available for containers

## Quick Start

### 1. Create Directory Structure

SSH into your NAS and create the required directories:

```bash
# Create app directories
sudo mkdir -p /volume1/docker/photos-index/{postgres,thumbnails,cleaner-logs,traefik}

# Set permissions (adjust user/group as needed)
sudo chown -R 1000:1000 /volume1/docker/photos-index
```

### 2. Download Configuration Files

```bash
cd /volume1/docker/photos-index

# Download compose files
wget https://raw.githubusercontent.com/YOUR_REPO/photos-index/main/deploy/docker/docker-compose.yml
wget https://raw.githubusercontent.com/YOUR_REPO/photos-index/main/deploy/docker/docker-compose.synology.yml

# Download and configure environment
wget -O .env https://raw.githubusercontent.com/YOUR_REPO/photos-index/main/deploy/docker/.env.synology
```

### 3. Configure Environment

Edit the `.env` file with your settings:

```bash
nano .env
```

**Critical settings:**

```env
# Change this to a secure password!
POSTGRES_PASSWORD=your_secure_password_here

# Set your photo directory path
HOST_PHOTOS_DIRECTORY=/volume1/photo
```

### 4. Start Services

```bash
# Using Docker Compose with Synology override
docker compose -f docker-compose.yml -f docker-compose.synology.yml up -d

# Check status
docker compose -f docker-compose.yml -f docker-compose.synology.yml ps
```

### 5. Access the Application

| Service | URL |
|---------|-----|
| Web Interface | http://your-nas-ip:8888 |
| Aspire Dashboard | http://your-nas-ip:18888 |
| API Direct | http://your-nas-ip:5000 |

## Photo Directory Configuration

### Single Directory

The simplest setup - index a single photo directory:

```env
HOST_PHOTOS_DIRECTORY=/volume1/photo
```

### Multiple Directories

**Option 1: Mount parent directory**

Mount a higher-level directory and configure specific paths in the web UI:

```env
HOST_PHOTOS_DIRECTORY=/volume1
```

Then in Settings, add scan directories:
- `/photos/photo` (maps to /volume1/photo)
- `/photos/homes/user/Photos` (maps to /volume1/homes/user/Photos)

**Option 2: Use symbolic links**

Create a directory with symlinks to all photo locations:

```bash
mkdir -p /volume1/photos-index-sources
ln -s /volume1/photo /volume1/photos-index-sources/main
ln -s /volume2/archive /volume1/photos-index-sources/archive
ln -s /volume1/homes/john/Photos /volume1/photos-index-sources/john
```

Then configure:

```env
HOST_PHOTOS_DIRECTORY=/volume1/photos-index-sources
```

## Read-Only vs Read-Write Access

### Indexing Service (Read-Only)

The indexing service only needs read access to scan and hash files:

```yaml
volumes:
  - ${HOST_PHOTOS_DIRECTORY}:/photos:ro  # Read-only
```

This is the default in `docker-compose.synology.yml`.

### Cleaner Service (Read-Write)

The cleaner service needs write access to move duplicates to trash:

```yaml
volumes:
  - ${HOST_PHOTOS_DIRECTORY}:/photos  # Read-write
```

**Safety Recommendations:**

1. Keep `CLEANER_ENABLED=false` initially
2. Review detected duplicates in the web UI
3. Only enable cleaner after verification
4. Cleaner uses soft delete (moves to trash, doesn't permanently delete)

## Resource Limits

The Synology override file includes memory limits suitable for NAS:

| Service | Memory Limit |
|---------|--------------|
| PostgreSQL | 512 MB |
| API | 256 MB |
| Indexing | 512 MB |
| Cleaner | 256 MB |
| Web | 128 MB |
| Aspire | 256 MB |
| Traefik | 128 MB |

**Total: ~2 GB**

Adjust in `docker-compose.synology.yml` if needed.

## Using Pre-built Images

For NAS deployment, building images locally is slow. Use pre-built images instead:

1. Build and push images from your development machine:

```bash
# Build and tag
docker build -t ghcr.io/youruser/photos-index-api:latest -f deploy/docker/api/Dockerfile .
docker build -t ghcr.io/youruser/photos-index-indexing:latest -f deploy/docker/indexing-service/Dockerfile .
docker build -t ghcr.io/youruser/photos-index-web:latest -f deploy/docker/web/Dockerfile .
docker build -t ghcr.io/youruser/photos-index-cleaner:latest -f deploy/docker/cleaner-service/Dockerfile .

# Push to registry
docker push ghcr.io/youruser/photos-index-api:latest
docker push ghcr.io/youruser/photos-index-indexing:latest
docker push ghcr.io/youruser/photos-index-web:latest
docker push ghcr.io/youruser/photos-index-cleaner:latest
```

2. Update `docker-compose.synology.yml` to use images:

```yaml
api:
  image: ghcr.io/youruser/photos-index-api:latest

indexing-service:
  image: ghcr.io/youruser/photos-index-indexing:latest
```

## Scheduled Indexing

The indexing service supports automatic scanning:

```env
# Scan interval in minutes
# 60 = hourly
# 1440 = daily (recommended)
# 10080 = weekly
INDEXING_INTERVAL_MINUTES=1440
```

## Troubleshooting

### Check Logs

```bash
# All services
docker compose -f docker-compose.yml -f docker-compose.synology.yml logs

# Specific service
docker compose -f docker-compose.yml -f docker-compose.synology.yml logs indexing-service

# Follow logs
docker compose -f docker-compose.yml -f docker-compose.synology.yml logs -f
```

### Database Issues

```bash
# Check PostgreSQL
docker exec -it photos-index-db psql -U photosuser -d photosindex -c "SELECT COUNT(*) FROM indexed_files;"
```

### Permission Issues

If the indexer can't read photos:

```bash
# Check container user
docker exec -it photos-index-indexing id

# Check file permissions
ls -la /volume1/photo
```

### Port Conflicts

If ports are already in use, modify in `.env`:

```env
TRAEFIK_HTTP_PORT=8889  # Change from 8888
API_PORT=5001           # Change from 5000
```

## Backup

### Database

```bash
# Backup
docker exec photos-index-db pg_dump -U photosuser photosindex > backup.sql

# Restore
docker exec -i photos-index-db psql -U photosuser photosindex < backup.sql
```

### Configuration

Backup these files:
- `.env`
- `docker-compose.yml`
- `docker-compose.synology.yml`

## Updating

```bash
cd /volume1/docker/photos-index

# Pull latest images
docker compose -f docker-compose.yml -f docker-compose.synology.yml pull

# Restart with new images
docker compose -f docker-compose.yml -f docker-compose.synology.yml up -d
```

## SSL/HTTPS Setup

For HTTPS with Let's Encrypt:

1. Uncomment SSL lines in `docker-compose.yml`
2. Set your email and domain in `.env`
3. Ensure ports 80 and 443 are accessible from internet

```env
ACME_EMAIL=your@email.com
```

## Performance Tips

1. **Use SSD cache** for PostgreSQL data if available
2. **Initial scan** may take hours for large libraries - this is normal
3. **Reduce trace sampling** in production:
   ```env
   TRAEFIK_TRACE_SAMPLE_RATE=0.1
   ```
4. **Disable access logging** to reduce I/O:
   ```env
   TRAEFIK_ACCESS_LOG=false
   ```
