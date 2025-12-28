# AI Photo Sorting Roadmap

**Status**: 📋 Planning
**Created**: 2025-12-28
**Goal**: Clean and organize 283K+ photos before Immich import

## Hardware Available

| Machine | CPU | GPU | RAM | Use Case |
|---------|-----|-----|-----|----------|
| Dell XPS 15 | i7 | RTX 3050 Ti (4GB) | 64 GB | AI inference (Ollama) |
| PC AMD | Ryzen 5600X | RX 580 (8GB) | 32 GB | Batch processing |
| TrueNAS | Server | - | - | Storage + API |

## Priority Roadmap

### P1: Near-Duplicate Detection (pHash)
**Effort**: 8-12h | **Impact**: High

Detect visually similar photos that hash-based dedup misses:
- Crops and resizes
- Screenshots of photos
- Filtered/edited versions
- Different compression levels

**Implementation**:
- Add ImageSharp perceptual hashing
- Store pHash in database alongside SHA256
- Group by Hamming distance threshold
- UI to review near-duplicate groups

**Files to modify**:
- `src/Shared/` - Add PerceptualHash to DTOs
- `src/Database/` - Add pHash column to IndexedFiles
- `src/IndexingService/` - Compute pHash during scan
- `src/Api/` - Endpoint for near-duplicate groups
- `src/Web/` - Near-duplicates viewer

---

### P2: Burst Detection
**Effort**: 4-6h | **Impact**: Medium

Detect rapid-fire photo sequences (same scene, seconds apart):
- Group photos taken within 5 seconds
- Same camera/device
- Similar composition (optional pHash check)

**Implementation**:
- Query by DateTaken proximity + CameraMake/Model
- Create BurstGroup entity
- Auto-select "best" photo (sharpest, eyes open - P4)
- UI to review bursts and pick keeper

**Database**:
```sql
-- Find bursts: photos within 5 seconds from same camera
SELECT * FROM indexed_files f1
JOIN indexed_files f2 ON f1.camera_make = f2.camera_make
WHERE ABS(EXTRACT(EPOCH FROM f1.date_taken - f2.date_taken)) < 5
```

---

### P3: AI Tagging (Ollama + Vision Models)
**Effort**: 16-24h | **Impact**: High

Auto-classify photos using local AI:
- Scene detection (beach, mountain, city, indoor)
- Object detection (car, dog, food, etc.)
- People count
- Activity (birthday, wedding, vacation)

**Models** (local, privacy-first):
| Model | VRAM | Speed | Use |
|-------|------|-------|-----|
| Moondream 2 (1.8B) | 4 GB | ~1s/img | Quick tagging |
| LLaVA 7B | 8 GB | 3-5s/img | Detailed descriptions |
| CLIP | 2 GB | <1s/img | Embeddings for search |

**Implementation**:
- New `AiTaggingService` worker
- RabbitMQ queue for batch processing
- Store tags in new `photo_tags` table
- API endpoint for tag search
- UI tag browser/filter

**Architecture**:
```
IndexedFile → RabbitMQ → AiTaggingService → Ollama API → Store tags
```

---

### P4: Quality Scoring
**Effort**: 8-12h | **Impact**: Medium

Score photos to auto-select best from duplicates/bursts:
- Sharpness (Laplacian variance)
- Exposure (histogram analysis)
- Face detection + eyes open
- Composition (rule of thirds)
- Resolution

**Implementation**:
- `QualityScorer` service
- Store scores in database
- Use for auto-selection in duplicate/burst groups
- "Best photos" filter in UI

**Scoring formula**:
```
score = (sharpness * 0.3) + (exposure * 0.2) + (faces_score * 0.3) + (resolution * 0.2)
```

---

### P5: Album Proposals
**Effort**: 12-16h | **Impact**: High

Auto-generate album suggestions:
- **By Event**: Cluster by date + location + time gaps
- **By Year**: Year-in-review with best photos
- **By Person**: Face clustering (future)
- **By Location**: GPS clustering + reverse geocoding

**Implementation**:
- Event detection algorithm (time gaps > 4h = new event)
- GPS clustering (DBSCAN)
- Best photo selection per event (P4 scores)
- "Proposed Albums" UI
- Export to Immich-compatible format

**Use cases**:
- Digital frame slideshows
- Year-in-review generation
- Photo book proposals
- Calendar creation

---

## Implementation Order

```
Phase 1: Foundation (v0.5.0)
├── P1: Near-duplicates (pHash)
└── P2: Burst detection

Phase 2: AI Core (v0.6.0)
├── P3: AI Tagging service
└── P4: Quality scoring

Phase 3: Smart Features (v0.7.0)
└── P5: Album proposals

Phase 4: Immich Migration
└── Export clean library → Immich
```

## Database Schema Additions

```sql
-- Perceptual hash for near-duplicate detection
ALTER TABLE indexed_files ADD COLUMN perceptual_hash BYTEA;
CREATE INDEX idx_files_phash ON indexed_files USING hash (perceptual_hash);

-- Quality scores
ALTER TABLE indexed_files ADD COLUMN quality_score DECIMAL(5,2);
ALTER TABLE indexed_files ADD COLUMN sharpness_score DECIMAL(5,2);
ALTER TABLE indexed_files ADD COLUMN exposure_score DECIMAL(5,2);

-- AI tags
CREATE TABLE photo_tags (
    id UUID PRIMARY KEY,
    indexed_file_id UUID REFERENCES indexed_files(id),
    tag VARCHAR(100),
    confidence DECIMAL(3,2),
    source VARCHAR(50), -- 'moondream', 'llava', 'manual'
    created_at TIMESTAMP DEFAULT NOW()
);

-- Burst groups
CREATE TABLE burst_groups (
    id UUID PRIMARY KEY,
    selected_file_id UUID REFERENCES indexed_files(id),
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE burst_group_files (
    burst_group_id UUID REFERENCES burst_groups(id),
    indexed_file_id UUID REFERENCES indexed_files(id),
    PRIMARY KEY (burst_group_id, indexed_file_id)
);

-- Album proposals
CREATE TABLE proposed_albums (
    id UUID PRIMARY KEY,
    name VARCHAR(255),
    type VARCHAR(50), -- 'event', 'year_review', 'location'
    start_date TIMESTAMP,
    end_date TIMESTAMP,
    status VARCHAR(20) DEFAULT 'proposed', -- 'proposed', 'accepted', 'rejected'
    created_at TIMESTAMP DEFAULT NOW()
);
```

## Success Metrics

| Metric | Target |
|--------|--------|
| Near-duplicates found | Reduce storage 10-20% |
| Bursts detected | Group 80%+ of rapid sequences |
| AI tag accuracy | >85% relevance |
| Album proposal acceptance | >50% useful |
| Time to process 283K photos | <24h on available hardware |

## Dependencies

- Ollama running on Dell XPS or PC AMD
- RabbitMQ for job queue (already in stack)
- ImageSharp for image analysis
- face_recognition or similar for face detection
