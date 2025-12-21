# Future Advanced Features Roadmap

**Created**: 2025-12-21
**Status**: Proposal / Discovery Phase

This document outlines potential advanced features for future versions (v0.3.0+) that would significantly enhance the photo indexing application with AI capabilities, smart albums, and cloud integration.

---

## Feature 1: AI-Powered Image Understanding

### Overview
Add semantic understanding of image content using AI models to enable:
- Auto-tagging (people, objects, scenes, activities)
- Natural language search ("sunset at the beach", "birthday party")
- Content-based duplicate detection (similar but not identical images)
- NSFW/sensitive content flagging

### Implementation Options

#### Option A: Local AI Model (Recommended for Privacy)

**Technology Stack:**
- [Ollama](https://ollama.com/) as the local inference server
- Vision models: LLaVA 1.6 (7B/13B), Moondream 2, or Llama 3.2 Vision (11B)
- Runs on dedicated local machine or NAS with GPU

**Hardware Requirements:**
| Model | VRAM | RAM | Response Time |
|-------|------|-----|---------------|
| Moondream 2 (1.8B) | 4GB | 8GB | ~1 second |
| LLaVA 7B | 8GB | 16GB | 2-5 seconds |
| LLaVA 13B | 16GB | 32GB | 5-10 seconds |
| LLaVA 34B | 24GB+ | 64GB | 10-30 seconds |

**Pros:**
- Complete privacy (images never leave your network)
- No ongoing API costs after hardware purchase
- Works offline
- No rate limits

**Cons:**
- Requires dedicated GPU hardware (RTX 3060+ recommended)
- Lower accuracy than cloud models
- Synology NAS likely too weak; needs separate compute node
- Model updates require manual intervention

**Rough Cost Estimate:**
- Hardware: $500-$1500 (used RTX 3060/3070 or new RTX 4060/4070)
- Power: ~$5-15/month depending on usage
- One-time setup effort: 8-16 hours

#### Option B: Mistral AI Cloud API

**Technology Stack:**
- Mistral API with Pixtral (12B vision model)
- Batch processing for cost efficiency

**Pricing (as of late 2024):**
| Model | Input | Output |
|-------|-------|--------|
| Pixtral 12B | Free (Apache 2.0, self-hosted) | - |
| Mistral Medium 3 | $0.40/1M tokens | $2.00/1M tokens |
| Mistral Large | $2.00/1M tokens | $6.00/1M tokens |

**Cost Estimate for Photo Library:**
- Assume ~500 tokens per image analysis
- 100,000 images = 50M tokens
- Using Medium 3: ~$20-30 one-time
- Monthly new photos (1000): ~$0.20-0.50/month

**Pros:**
- High accuracy
- No hardware investment
- Always up-to-date models
- Simple integration

**Cons:**
- Privacy concerns (images sent to cloud)
- Ongoing costs (though minimal)
- Requires internet connection
- Rate limits may apply

#### Option C: Infomaniak Swiss AI (Sovereign European)

**Technology Stack:**
- [Infomaniak AI Tools](https://www.infomaniak.com/en/hosting/ai-tools) API
- Swiss-hosted, GDPR compliant
- Uses open-source models (Mixtral for text, Flux/SDXL for images)

**Pricing:**
- Usage-based billing (per token/per minute)
- 1 million free credits for testing (1 month trial)
- Competitive pricing vs. US providers
- Vision/image interpretation: Currently in development (as of late 2024)

**Pros:**
- Data sovereignty (Swiss + EU data protection laws)
- No data logging or storage of requests
- Ethical AI focus
- Supports open-source models
- European hosting (lower latency for EU users)

**Cons:**
- Vision models not yet fully available (in analysis phase)
- Smaller ecosystem than US providers
- May have fewer model options

**Best For:**
- European users with strict data sovereignty requirements
- GDPR-sensitive use cases
- Users preferring open-source models with cloud convenience

**Note:** Monitor Infomaniak's roadmap for vision model availability before committing to this option for image understanding.

#### Option D: Hybrid Approach

Use local model for initial tagging, cloud API for verification or complex queries.

### Effort Estimate
| Component | Effort | Complexity |
|-----------|--------|------------|
| Database schema for tags/embeddings | 4-6h | Medium |
| Ollama integration service | 8-12h | Medium |
| Mistral API client | 4-6h | Low |
| Background processing pipeline | 8-12h | High |
| Vector search (similarity) | 12-16h | High |
| Web UI for tags/search | 8-12h | Medium |
| **Total** | **44-64h** | - |

### Library Sizing (Reference: 100K+ Photos)

Typical family photo library spanning 40+ years:
| Period | Approx. Photos | Priority |
|--------|----------------|----------|
| Children's lives (7 years) | 30-50K | High - process first |
| Married life (15 years) | 40-60K | Medium |
| Lifetime archive (40+ years) | 20-40K | Low - historical |
| **Total** | **100-150K** | |

**Recommended Processing Strategy: Newest First**
1. Start with most recent year (highest value, freshest memories)
2. Work backwards chronologically
3. Prioritize directories with highest photo density
4. Children/family albums before solo/travel archives

### Processing Time Estimates (100K Photos)

| Method | Time/Image | Total Time | Notes |
|--------|------------|------------|-------|
| Local (Moondream 2) | ~1s | ~28 hours | Lightweight, fast |
| Local (LLaVA 7B) | ~3s | ~3.5 days | Better quality |
| Local (LLaVA 13B) | ~7s | ~8 days | High quality |
| Cloud (Mistral batch) | ~0.5s | ~14 hours | Rate-limited bursts |

**Practical approach**: Process 1000 newest photos/day = 100 days for full backfill, or dedicate a weekend for intensive processing.

### Cost Estimates (100K Photos)

| Provider | Model | Input Cost | Output Cost | Total |
|----------|-------|------------|-------------|-------|
| Mistral | Medium 3 | ~$20 | ~$100 | ~$120 |
| Mistral | Pixtral (self-hosted) | Free | - | Hardware only |
| Infomaniak | TBD | TBD | TBD | Monitor pricing |

### Key Pain Points
1. **Initial backfill time**: 100K images needs dedicated processing window
2. **Storage**: Embeddings add ~2KB per image (~200MB for 100K)
3. **Hardware cost**: Dedicated GPU machine for local option
4. **API quotas**: Cloud providers may have rate limits
5. **Model updates**: Local models need manual updates; cloud models may change
6. **Prioritization**: Need smart queue management to process high-value photos first

---

## Feature 2: Dynamic Smart Albums

### Overview
Automatically create and maintain albums based on:
- Temporal patterns (holidays, seasons, years)
- Location clusters (vacations, home, work)
- People groupings (via face recognition)
- Events (parties, weddings, graduations)
- Activities (hiking, beach, snow)

### Sub-Features

#### 2a. Temporal Albums
- "Christmas 2024", "Summer 2023", "Q3 2024"
- Automatically detect holiday periods from photo bursts
- Recognize annual recurring events (birthdays)

#### 2b. Location Albums
- Cluster GPS coordinates into locations
- Reverse geocode to readable names
- "Paris Trip 2024", "Grandma's House", "Office"

#### 2c. Event Detection
- Photo burst analysis (many photos in short time = event)
- Cross-reference with calendar (if integrated)
- Social event detection (multiple people, indoor/outdoor)

#### 2d. People Albums
- Face detection and clustering
- Named person albums
- "Photos with Mom", "Kids growing up"

### Effort Estimate
| Component | Effort | Complexity | Dependencies |
|-----------|--------|------------|--------------|
| Temporal clustering algorithm | 6-8h | Medium | Extended EXIF |
| GPS clustering + reverse geocoding | 8-12h | Medium | GPS data in DB |
| Event burst detection | 4-6h | Low | DateTaken data |
| Face detection (local) | 16-24h | High | AI infrastructure |
| Face clustering/matching | 12-16h | High | Face detection |
| Album management API | 8-12h | Medium | - |
| Album Web UI | 12-16h | Medium | - |
| **Total (basic)** | **26-38h** | - | Without faces |
| **Total (with faces)** | **66-94h** | - | Full feature |

### Key Pain Points
1. **Face recognition privacy**: Sensitive data, needs careful handling
2. **GPS availability**: Many photos lack GPS data
3. **Calendar integration**: Requires OAuth with Google/Apple
4. **Clustering accuracy**: Subjective what constitutes an "event"
5. **Performance**: Clustering 1M+ photos is compute-intensive

---

## Feature 3: Album Proposals for Products

### Overview
Generate ready-to-use album suggestions for:
- **Calendar creation**: 12 best photos for a wall calendar
- **Holiday photo books**: Curated vacation albums
- **Digital frame rotation**: Diverse, high-quality selection
- **Year in review**: Highlights from the past year

### Sub-Features

#### 3a. Quality Scoring
- Blur detection (reject blurry photos)
- Exposure analysis (reject over/underexposed)
- Composition scoring (rule of thirds, etc.)
- Face detection (prefer photos with faces)
- Duplicate/similar filtering

#### 3b. Diversity Selection
- Temporal spread (not all from one day)
- Subject variety (people, places, activities)
- Location diversity
- Season representation

#### 3c. Product Templates
| Product | Photos Needed | Criteria |
|---------|---------------|----------|
| Wall Calendar | 12-13 | Monthly variety, landscape orientation |
| Photo Book (small) | 20-40 | Story arc, event-focused |
| Photo Book (large) | 60-100 | Comprehensive, high quality |
| Digital Frame | 50-200 | High variety, auto-refresh |
| Year Review | 12-52 | Best moments, temporal spread |

### Effort Estimate
| Component | Effort | Complexity | Dependencies |
|-----------|--------|------------|--------------|
| Image quality scoring | 8-12h | Medium | AI or heuristics |
| Diversity algorithm | 6-8h | Medium | Metadata |
| Template definitions | 4-6h | Low | - |
| Proposal generation API | 6-8h | Medium | Above components |
| Preview/export Web UI | 12-16h | Medium | - |
| Export formats (PDF, ZIP) | 6-8h | Low | - |
| **Total** | **42-58h** | - | - |

### Key Pain Points
1. **Subjective quality**: What's "good" is personal
2. **Processing overhead**: Quality analysis needs AI or complex heuristics
3. **Export integration**: Different services have different requirements
4. **User override**: Must allow manual curation of suggestions

---

## Feature 4: Cloud Storage Indexing

### Overview
Index photos from cloud storage providers:
- Apple iCloud Photos
- Microsoft OneDrive
- Google Photos
- Amazon Photos

### Implementation Options

#### 4a. iCloud Integration

**Approach:**
- Use iCloud for Windows/Linux or pyicloud library
- Download metadata + thumbnails only
- Full image download on-demand

**Challenges:**
| Issue | Severity | Mitigation |
|-------|----------|------------|
| No official API | Critical | Use unofficial libraries, may break |
| 2FA required | High | Requires user intervention periodically |
| HEIC format | Medium | Convert or add support |
| Live Photos | Medium | Handle as image+video pair |
| Rate limiting | High | Gentle crawling, caching |

**Legal/TOS Concerns:**
- Apple TOS may prohibit automated access
- Account lockout risk if detected

#### 4b. OneDrive Integration

**Approach:**
- Official Microsoft Graph API
- OAuth 2.0 authentication
- Well-documented, stable API

**Advantages:**
- Official support, won't break
- Granular permissions
- Webhook support for changes

**Challenges:**
| Issue | Severity | Mitigation |
|-------|----------|------------|
| OAuth complexity | Medium | Use MSAL library |
| Token refresh | Low | Handled by SDK |
| API rate limits | Medium | Batch requests, respect limits |
| Large libraries | Medium | Incremental sync |

#### 4c. Deduplication Across Local + Cloud

**Strategy Options:**
1. **Hash-based**: Download each file to compute SHA256 (expensive)
2. **Metadata-based**: Match by name + size + date (fast but less accurate)
3. **Content-based**: Use AI embeddings (requires processing)

**Recommendation:**
- Start with metadata matching
- Add hash verification for candidates
- Use cloud APIs to avoid downloading everything

### Effort Estimate
| Component | Effort | Complexity | Dependencies |
|-----------|--------|------------|--------------|
| iCloud client integration | 16-24h | High | Unofficial APIs |
| OneDrive Graph API client | 8-12h | Medium | OAuth setup |
| Cloud file entity in DB | 4-6h | Low | - |
| Incremental sync service | 12-16h | High | - |
| Cross-source deduplication | 12-16h | High | Hash + metadata |
| Settings UI for cloud accounts | 8-12h | Medium | - |
| **iCloud only** | **40-58h** | High | Fragile |
| **OneDrive only** | **32-46h** | Medium | Stable |
| **Both + dedup** | **60-86h** | High | - |

### Key Pain Points
1. **iCloud instability**: No official API, Apple may block
2. **Authentication UX**: OAuth flows, 2FA handling
3. **Sync state management**: Tracking what's been indexed
4. **Storage costs**: May need to download for hashing
5. **Privacy**: Storing cloud credentials securely
6. **Rate limits**: All providers have limits

---

## Priority Recommendation

Based on effort, value, and risk:

| Priority | Feature | Rationale |
|----------|---------|-----------|
| 1 | Dynamic Albums (basic temporal) | Low effort, high value, no dependencies |
| 2 | AI Understanding (Mistral/Infomaniak) | Medium effort, transformative value |
| 3 | Album Proposals | Builds on AI + albums, practical output |
| 4 | OneDrive Integration | Stable API, many users |
| 5 | AI Understanding (Local) | Hardware barrier, but privacy win |
| 6 | iCloud Integration | High risk, fragile, but popular |
| 7 | Face Recognition | High effort, privacy concerns |

**Note:** For European users with data sovereignty requirements, Infomaniak Swiss AI is recommended once their vision models become available.

---

## Summary Table

| Feature | Dev Effort | Processing Time (100K) | Cost | Main Blocker |
|---------|------------|------------------------|------|--------------|
| AI (Local) | 44-64h | 1-8 days | Hardware $500-1500 | GPU required |
| AI (Cloud) | 44-64h | 14-28 hours | ~$120 one-time | Rate limits |
| Smart Albums (basic) | 26-38h | Minutes | Free | Metadata quality |
| Smart Albums (faces) | 66-94h | 2-4 days | Free | Privacy |
| Album Proposals | 42-58h | Seconds | Free | Needs AI first |
| iCloud Indexing | 40-58h | Days (download) | Free | No official API |
| OneDrive Indexing | 32-46h | Hours | Free | OAuth setup |

---

## Reference Library Context

This roadmap is sized for a typical family photo library:

```
Timeline: 40+ years of photos
├── Recent (0-7 years)   → Children, high activity  → 30-50K photos (HIGH priority)
├── Middle (7-15 years)  → Married life, events     → 40-60K photos (MEDIUM priority)
└── Archive (15-40 years)→ Historical, nostalgia    → 20-40K photos (LOW priority)

Total: 100-150K photos
```

**Key insight**: Most value is in recent photos. A "newest first" strategy delivers immediate benefit while backfill continues in background.

---

## References

- [Ollama Vision Models](https://ollama.com/blog/vision-models)
- [LLaVA Documentation](https://llava-vl.github.io/)
- [Mistral AI Pricing](https://mistral.ai/pricing)
- [Infomaniak AI Tools](https://www.infomaniak.com/en/hosting/ai-tools)
- [Microsoft Graph API - OneDrive](https://learn.microsoft.com/en-us/onedrive/developer/)
- [pyicloud (unofficial iCloud)](https://github.com/picklepete/pyicloud)
