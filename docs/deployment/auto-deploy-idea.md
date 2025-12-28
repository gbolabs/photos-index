# Auto-Deploy to NAS - Ideas

## Current State

Manual deployment:
1. Release tagged on GitHub
2. CI builds Docker images → GHCR
3. User manually pulls images on NAS
4. User manually restarts containers

## Proposed Solutions

### Option 1: Watchtower (Simplest)

Run [Watchtower](https://containrrr.dev/watchtower/) on each NAS to auto-pull new images.

**TrueNAS:**
```yaml
# Add to truenas-0.4.x.yml
watchtower:
  image: containrrr/watchtower
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock
  environment:
    WATCHTOWER_POLL_INTERVAL: 300  # Check every 5 min
    WATCHTOWER_CLEANUP: "true"
    WATCHTOWER_INCLUDE_STOPPED: "true"
    WATCHTOWER_LABEL_ENABLE: "true"  # Only update labeled containers
  restart: unless-stopped
```

Add to each service:
```yaml
labels:
  - com.centurylinklabs.watchtower.enable=true
```

**Pros:**
- Simple setup
- No external dependencies
- Works with GHCR

**Cons:**
- Polls (not instant)
- All-or-nothing updates
- No rollback

---

### Option 2: GitHub Actions + SSH Deploy

Add SSH deploy step to release workflow.

**.github/workflows/release.yml:**
```yaml
deploy-truenas:
  needs: build
  runs-on: ubuntu-latest
  steps:
    - name: Deploy to TrueNAS
      uses: appleboy/ssh-action@v1
      with:
        host: ${{ secrets.TRUENAS_HOST }}
        username: ${{ secrets.TRUENAS_USER }}
        key: ${{ secrets.TRUENAS_SSH_KEY }}
        script: |
          cd /path/to/compose
          docker compose pull
          docker compose up -d

deploy-synology:
  needs: build
  runs-on: ubuntu-latest
  steps:
    - name: Deploy to Synology
      uses: appleboy/ssh-action@v1
      with:
        host: ${{ secrets.SYNOLOGY_HOST }}
        username: ${{ secrets.SYNOLOGY_USER }}
        key: ${{ secrets.SYNOLOGY_SSH_KEY }}
        script: |
          cd /path/to/compose
          docker compose pull
          docker compose up -d
```

**Secrets required:**
- `TRUENAS_HOST`, `TRUENAS_USER`, `TRUENAS_SSH_KEY`
- `SYNOLOGY_HOST`, `SYNOLOGY_USER`, `SYNOLOGY_SSH_KEY`

**Pros:**
- Instant deploy on release
- Controlled from GitHub
- Can add health checks

**Cons:**
- Requires SSH access from GitHub
- Security: SSH key in GitHub secrets
- Network exposure

---

### Option 3: Webhook + Local Script

GitHub sends webhook on release → Local script pulls and deploys.

**On NAS (webhook listener):**
```bash
#!/bin/bash
# deploy-webhook.sh - Run with webhook server like `webhook`

cd /path/to/compose
docker compose pull
docker compose up -d
```

**Webhook server:**
```yaml
# webhook config
- id: deploy
  execute-command: /path/to/deploy-webhook.sh
  trigger-rule:
    match:
      type: payload-hmac-sha256
      secret: "{{ .WEBHOOK_SECRET }}"
      parameter:
        source: header
        name: X-Hub-Signature-256
```

**Pros:**
- No SSH exposure
- NAS pulls (outbound only)
- Works behind NAT

**Cons:**
- Requires webhook server on NAS
- More complex setup

---

### Option 4: GitHub Container Registry Webhook + ArgoCD/Flux (Overkill)

For Kubernetes-style GitOps. Probably overkill for 2 NAS.

---

## Recommendation

**For this project: Option 1 (Watchtower)**

1. Simplest to set up
2. No external dependencies
3. Works with private GHCR (with auth)
4. Good enough for home lab

**Setup:**

1. Add Watchtower to both compose files
2. Add `watchtower.enable=true` labels to services
3. Configure GHCR auth for private images (if needed)

## Implementation Plan

1. [ ] Add Watchtower service to `truenas-0.4.x.yml`
2. [ ] Add Watchtower service to `synology-0.4.x.yml`
3. [ ] Add `watchtower.enable` labels to all services
4. [ ] Test auto-update on next release
5. [ ] Document rollback procedure (manual)

## Alternative: Semi-Auto

Keep manual control but simplify:

```bash
# deploy.sh on each NAS
#!/bin/bash
VERSION=${1:-latest}
docker compose pull
docker compose up -d
echo "Deployed version: $VERSION"
```

Then just SSH and run:
```bash
ssh truenas "./deploy.sh 0.4.2"
```
