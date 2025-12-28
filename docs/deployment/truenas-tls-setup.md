# TrueNAS TLS Certificate Setup

## Overview

This guide explains how to configure TLS certificates for the Photos Index stack on TrueNAS.

## Port Configuration

| Protocol | Port | Traefik Entrypoint | Usage |
|----------|------|-------------------|-------|
| HTTP | 8050 | `web` | Unencrypted access |
| **HTTPS** | **8053** | `websecure` | TLS encrypted access |
| Traefik Dashboard | 8054 | - | Internal dashboard |

**Important**: Use port `8053` for HTTPS, not `8050`.

## URLs

```
HTTP:  http://tn.isago.ch:8050
HTTPS: https://tn.isago.ch:8053
```

## Setup Procedure

### 1. Create directories

```bash
mkdir -p /mnt/hdr1/apps/photos-index/certs
mkdir -p /mnt/hdr1/apps/photos-index/traefik
```

### 2. Copy certificates

Copy your certificate and private key:

```bash
cp /path/to/your-domain.crt /mnt/hdr1/apps/photos-index/certs/isago.ch.crt
cp /path/to/your-domain.key /mnt/hdr1/apps/photos-index/certs/isago.ch.key

# Restrict permissions on private key
chmod 600 /mnt/hdr1/apps/photos-index/certs/isago.ch.key
```

### 3. Create Traefik TLS configuration

```bash
cat > /mnt/hdr1/apps/photos-index/traefik/tls.yml << 'EOF'
tls:
  certificates:
    - certFile: /certs/isago.ch.crt
      keyFile: /certs/isago.ch.key
  stores:
    default:
      defaultCertificate:
        certFile: /certs/isago.ch.crt
        keyFile: /certs/isago.ch.key
EOF
```

### 4. Verify file structure

```
/mnt/hdr1/apps/photos-index/
├── certs/
│   ├── isago.ch.crt      # Certificate (public)
│   └── isago.ch.key      # Private key (chmod 600)
├── traefik/
│   └── tls.yml           # Traefik dynamic config
├── grafana/
├── jaeger/
├── loki/
└── minio/
```

### 5. Restart Traefik

```bash
cd /path/to/compose
docker compose -f truenas-0.4.1.yml restart traefik
```

### 6. Verify TLS

```bash
# Test HTTPS endpoint
curl -v https://tn.isago.ch:8053/health

# Check certificate details
openssl s_client -connect tn.isago.ch:8053 -servername tn.isago.ch < /dev/null 2>/dev/null | openssl x509 -noout -subject -dates
```

## Troubleshooting

### 404 on HTTPS

**Symptom**: `https://tn.isago.ch:8050` returns 404

**Cause**: Port 8050 is HTTP only. HTTPS requires port 8053.

**Solution**: Use `https://tn.isago.ch:8053`

### Certificate not loading

**Check Traefik logs**:
```bash
docker logs photos-index-traefik 2>&1 | grep -i tls
```

**Verify file permissions**:
```bash
ls -la /mnt/hdr1/apps/photos-index/certs/
# Key should be readable by Docker (chmod 600)
```

**Verify dynamic config is loaded**:
```bash
# Check Traefik dashboard at http://tn.isago.ch:8050/traefik
# Look for TLS configuration in the dashboard
```

### Self-signed certificate warning

If using a self-signed certificate, browsers will show a warning. To test with curl:

```bash
curl -k https://tn.isago.ch:8053/health
```

## Certificate Sources

### Option 1: Let's Encrypt (recommended for public domains)

Use certbot or ACME to obtain free certificates.

### Option 2: Internal CA

For internal domains, use your organization's CA.

### Option 3: Self-signed (development only)

```bash
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout /mnt/hdr1/apps/photos-index/certs/isago.ch.key \
  -out /mnt/hdr1/apps/photos-index/certs/isago.ch.crt \
  -subj "/CN=tn.isago.ch"
```

## Volume Mounts in Docker Compose

The `truenas-0.4.1.yml` compose file mounts:

```yaml
traefik:
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock:ro
    - /mnt/hdr1/apps/photos-index/certs:/certs:ro
    - /mnt/hdr1/apps/photos-index/traefik:/etc/traefik/dynamic:ro
```

Traefik watches `/etc/traefik/dynamic` for changes and automatically reloads TLS configuration.
