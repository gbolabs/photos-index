# AI-Powered Metadata Synchronization

**Created**: 2025-12-23
**Status**: Proposed
**Priority**: Medium
**Dependencies**: AI infrastructure (Feature 1)

## Overview

Extend metadata from nearby images to photos that lack complete information using AI-powered detection and proximity analysis. This addresses the common scenario where:
- A smartphone photo has GPS location but no EXIF metadata
- A DSLR photo has rich EXIF but no GPS
- A WhatsApp received photo has no metadata at all
- Multiple photos from the same event have partial information

## Problem Statement

Many photos lack complete metadata due to:
- Mobile apps stripping EXIF data
- Social media platforms removing metadata
- Camera settings not recording GPS
- File format conversions losing information

However, when multiple photos are taken at the same event/location, they often have overlapping information that can be shared.

## Proposed Solution

### Core Feature: Metadata Synchronization

1. **Detect nearby photos** using:
   - Temporal proximity (photos taken within X minutes of each other)
   - Spatial proximity (photos with GPS coordinates within Y meters)
   - Visual similarity (perceptual hashing to identify same event)
   - Directory proximity (photos in the same folder/subfolder)

2. **AI-powered metadata transfer**:
   - Use AI vision models to detect if photos are from the same event
   - Analyze scene consistency (same location, similar lighting, etc.)
   - Transfer missing metadata from "source" photos to "target" photos

3. **Metadata fields that can be synchronized**:
   - GPS coordinates (latitude, longitude)
   - Date/Time (if missing or inconsistent)
   - Camera make/model (for consistency)
   - Exposure settings (ISO, aperture, shutter speed)
   - Location name (reverse geocoded from GPS)
   - Event tags (e.g., "beach vacation", "birthday party")

### User Workflow

1. **Manual Selection**: User selects a group of photos that should be synchronized
2. **Auto-Detection**: System suggests groups based on proximity analysis
3. **Preview**: Show what metadata will be transferred where
4. **Apply**: Transfer metadata with backup
5. **Revert**: Option to revert changes if needed

## Implementation Details

### Database Changes

```sql
-- Add to IndexedFile table
ALTER TABLE "IndexedFiles" ADD COLUMN "MetadataSyncGroupId" INTEGER;
ALTER TABLE "IndexedFiles" ADD COLUMN "MetadataSyncSourceId" INTEGER NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "MetadataSyncTimestamp" TIMESTAMPTZ NULL;
ALTER TABLE "IndexedFiles" ADD COLUMN "MetadataBackup" JSONB NULL;

-- New table for sync groups
CREATE TABLE "MetadataSyncGroups" (
    "Id" SERIAL PRIMARY KEY,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UserCreated" BOOLEAN NOT NULL DEFAULT FALSE,
    "Status" TEXT NOT NULL -- 'pending', 'applied', 'reverted'
);

-- Relationship table
CREATE TABLE "MetadataSyncGroupFiles" (
    "SyncGroupId" INTEGER NOT NULL REFERENCES "MetadataSyncGroups"("Id"),
    "IndexedFileId" INTEGER NOT NULL REFERENCES "IndexedFiles"("Id"),
    "Priority" INTEGER NOT NULL DEFAULT 0, -- 0 = target, 1 = source
    "IsSource" BOOLEAN NOT NULL DEFAULT FALSE
);
```

### API Endpoints

```
POST /api/metadata-sync/groups
  - Create a new sync group with selected files
  
GET /api/metadata-sync/groups
  - List all sync groups with status
  
GET /api/metadata-sync/groups/{id}
  - Get sync group details with preview of changes
  
POST /api/metadata-sync/groups/{id}/apply
  - Apply metadata synchronization
  
POST /api/metadata-sync/groups/{id}/revert
  - Revert metadata to original state
  
GET /api/metadata-sync/suggestions
  - Get AI-generated suggestions for sync groups
  
POST /api/metadata-sync/auto-detect
  - Auto-detect potential sync groups in directory
```

### Web UI Components

1. **Metadata Sync Dashboard**:
   - List of sync groups with status
   - Quick actions (apply, revert, delete)

2. **Sync Group Creator**:
   - File selection interface
   - Source/target designation
   - Preview of changes
   - Confirmation dialog

3. **Auto-Detection View**:
   - AI-generated suggestions
   - Confidence scores
   - Manual override options

4. **Metadata Detail Panel**:
   - Show sync history
   - Original vs. current metadata
   - Revert option

### Algorithm Details

#### Proximity Detection

```csharp
// Temporal proximity (within 30 minutes)
bool IsTemporallyClose(IndexedFile a, IndexedFile b, TimeSpan maxDelta = TimeSpan.FromMinutes(30))
{
    return Math.Abs((a.DateTaken ?? DateTime.MinValue) - 
                   (b.DateTaken ?? DateTime.MinValue)).TotalMinutes <= maxDelta.TotalMinutes;
}

// Spatial proximity (within 500 meters)
bool IsSpatiallyClose(IndexedFile a, IndexedFile b, double maxDistanceMeters = 500)
{
    if (a.Latitude == null || a.Longitude == null || 
        b.Latitude == null || b.Longitude == null)
        return false;
    
    // Haversine formula for distance calculation
    return CalculateHaversineDistance(a.Latitude.Value, a.Longitude.Value, 
                                     b.Latitude.Value, b.Longitude.Value) <= maxDistanceMeters;
}

// Visual similarity (using perceptual hash)
bool IsVisuallySimilar(IndexedFile a, IndexedFile b, int maxHammingDistance = 10)
{
    if (string.IsNullOrEmpty(a.PerceptualHash) || string.IsNullOrEmpty(b.PerceptualHash))
        return false;
    
    return CalculateHammingDistance(a.PerceptualHash, b.PerceptualHash) <= maxHammingDistance;
}
```

#### AI-Powered Scene Analysis

```csharp
// Using Ollama/Mistral API
async Task<SceneAnalysisResult> AnalyzeSceneForSync(IndexedFile[] files)
{
    // Send batch of images to AI model
    var response = await aiClient.AnalyzeImagesAsync(files.Select(f => f.FilePath));
    
    // Check for scene consistency
    bool sameLocation = response.All(r => r.LocationConfidence > 0.7);
    bool sameTimeOfDay = response.All(r => r.TimeOfDay == response[0].TimeOfDay);
    bool similarSubjects = response.All(r => r.SubjectCategory == response[0].SubjectCategory);
    
    return new SceneAnalysisResult
    {
        IsSameEvent = sameLocation && sameTimeOfDay && similarSubjects,
        Confidence = CalculateConfidence(sameLocation, sameTimeOfDay, similarSubjects),
        SuggestedSourceFiles = response
            .Where(r => r.HasGps && r.HasExif)
            .OrderByDescending(r => r.Confidence)
            .Select(r => r.FileId)
            .ToList()
    };
}
```

#### Metadata Transfer Rules

**Priority Order for Source Selection:**
1. Photos with GPS + EXIF data
2. Photos with GPS only
3. Photos with EXIF only
4. Photos with AI-detected metadata

**Field Transfer Rules:**
- GPS coordinates: Transfer if target has none
- Date/Time: Transfer if target is missing or differs by >5 minutes
- Camera info: Transfer if target is missing
- Exposure settings: Transfer if target is missing
- Location name: Always transfer if available
- AI tags: Merge with existing tags

### Backup & Revert System

```csharp
// Before applying changes
public void CreateMetadataBackup(IndexedFile file)
{
    file.MetadataBackup = JsonSerializer.Serialize(new
    {
        file.Latitude,
        file.Longitude,
        file.DateTaken,
        file.CameraMake,
        file.CameraModel,
        file.Iso,
        file.Aperture,
        file.ShutterSpeed,
        file.FocalLength,
        file.LensModel,
        file.Tags
    });
    file.MetadataSyncTimestamp = DateTime.UtcNow;
}

// Revert to original
public void RevertMetadata(IndexedFile file)
{
    if (file.MetadataBackup == null) return;
    
    var backup = JsonSerializer.Deserialize<MetadataBackup>(file.MetadataBackup);
    
    // Restore all fields
    file.Latitude = backup.Latitude;
    file.Longitude = backup.Longitude;
    file.DateTaken = backup.DateTaken;
    file.CameraMake = backup.CameraMake;
    file.CameraModel = backup.CameraModel;
    file.Iso = backup.Iso;
    file.Aperture = backup.Aperture;
    file.ShutterSpeed = backup.ShutterSpeed;
    file.FocalLength = backup.FocalLength;
    file.LensModel = backup.LensModel;
    file.Tags = backup.Tags;
    
    file.MetadataBackup = null;
    file.MetadataSyncGroupId = null;
    file.MetadataSyncSourceId = null;
}
```

## User Experience Considerations

### Visual Design

1. **Sync Group Card**:
   ```
   [Preview Images] [Group Name] [Status Badge]
   Source: [Camera Icon] [Count] | Target: [Count]
   Confidence: [Meter] | Last Updated: [Date]
   [Actions: Apply | Revert | Delete]
   ```

2. **Change Preview**:
   - Side-by-side comparison
   - Green (+) for added fields
   - Red (-) for removed fields
   - Blue (=) for unchanged fields

3. **Confidence Indicators**:
   - 🟢 High (90%+): Solid green
   - 🟡 Medium (70-90%): Yellow with warning
   - 🔴 Low (<70%): Red with manual override option

### Workflow Examples

#### Example 1: GPS Transfer
```
User has:
- Photo A: Taken with iPhone (has GPS, no EXIF)
- Photo B: Taken with DSLR (has EXIF, no GPS)

System detects:
- Both taken within 2 minutes
- Both in same location (AI analysis)
- Photo B has better EXIF data

Suggested sync:
- Transfer GPS from A to B
- Keep EXIF from B
```

#### Example 2: Multi-Photo Event
```
User has 15 photos from a birthday party:
- 5 photos: Full metadata (GPS + EXIF)
- 5 photos: GPS only (mobile)
- 3 photos: EXIF only (DSLR)
- 2 photos: No metadata (WhatsApp)

System creates sync group:
- Sources: 5 photos with full metadata
- Targets: 10 photos missing data
- Transfers appropriate fields to each
```

#### Example 3: Manual Override
```
User has two photos:
- Photo A: GPS shows "Paris"
- Photo B: GPS shows "London" (incorrect, same event)

System detects:
- Both taken within 1 hour
- Visually similar (same people)
- Suggests sync with low confidence

User manually:
- Overrides GPS for Photo B
- Confirms sync with custom location
```

## Technical Challenges

### 1. Conflict Resolution
- **Scenario**: Two source photos have conflicting GPS data
- **Solution**: Use AI to determine most likely correct location, flag for user review

### 2. Data Integrity
- **Scenario**: User reverts, then applies again
- **Solution**: Chain of custody tracking, version history

### 3. Performance
- **Scenario**: 10,000 photos in a directory
- **Solution**: Incremental analysis, batch processing, progress tracking

### 4. Privacy
- **Scenario**: User doesn't want AI analyzing photos
- **Solution**: Opt-in feature, clear privacy warnings, local-only mode

### 5. Accuracy
- **Scenario**: AI incorrectly groups photos
- **Solution**: Confidence thresholds, user review, manual override

## Effort Estimate

| Component | Effort | Complexity | Notes |
|-----------|--------|------------|-------|
| Database schema | 4-6h | Low | Simple additions |
| API endpoints | 8-12h | Medium | CRUD + preview |
| Sync algorithm | 12-16h | High | Proximity + AI logic |
| Backup system | 6-8h | Medium | JSON serialization |
| Web UI | 16-20h | Medium | Dashboard + forms |
| AI integration | 8-12h | High | Scene analysis |
| Testing | 8-12h | Medium | Edge cases |
| **Total** | **62-86h** | - | - |

## Dependencies

1. **AI Infrastructure** (Feature 1): Required for scene analysis
2. **Extended Metadata** (P2): Needed for EXIF fields
3. **Perceptual Hashing** (P4): Helps with visual similarity
4. **Async Job Queue** (P3): For batch processing large libraries

## Alternative Approaches

### Option A: Simple Metadata Copy (No AI)
- **Pros**: Faster to implement, no AI dependencies
- **Cons**: Less accurate, more false positives
- **Effort**: 30-40h

### Option B: Full AI Analysis (Slow)
- **Pros**: Most accurate, handles edge cases
- **Cons**: Very slow for large libraries, expensive
- **Effort**: 80-100h

### Option C: Hybrid (Recommended)
- **Fast path**: Temporal + spatial proximity (no AI)
- **Slow path**: AI analysis for ambiguous cases
- **Pros**: Good balance of speed and accuracy
- **Cons**: More complex implementation
- **Effort**: 62-86h (current estimate)

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| AI misclassification | Medium | High | Confidence thresholds, user review |
| Data corruption | Low | Critical | Full backup before apply |
| Performance issues | Medium | Medium | Batch processing, progress tracking |
| Privacy concerns | Low | High | Opt-in, clear warnings |
| User confusion | Medium | Medium | Clear UI, tutorials |

## Success Metrics

1. **Adoption**: % of users who try the feature
2. **Accuracy**: % of syncs that don't need manual correction
3. **Performance**: Time to analyze 1000 photos
4. **Satisfaction**: User feedback on usability

## Related Features

This feature complements:
- **Smart Albums**: Better metadata enables better grouping
- **Map View**: More photos with GPS improves visualization
- **Search**: Better metadata improves search results
- **Duplicate Detection**: Metadata consistency helps identify duplicates

## Future Enhancements

1. **Batch Processing**: Process entire directories automatically
2. **Recurring Sync**: Periodically check for new photos to sync
3. **Cloud Sync**: Sync metadata across devices
4. **Collaborative Editing**: Multiple users can contribute metadata
5. **Machine Learning**: Improve accuracy over time with user feedback

## References

- [Haversine Distance Formula](https://en.wikipedia.org/wiki/Haversine_formula)
- [Perceptual Hashing](https://www.hackerfactor.com/blog/index.php?/archives/432-Looks-Like-It.html)
- [EXIF Data Standards](https://exiftool.org/TagNames/EXIF.html)
- [Ollama Vision Models](https://ollama.com/blog/vision-models)
- [Mistral AI Image Analysis](https://docs.mistral.ai/capabilities/vision/)

## Open Questions

1. Should sync groups be persistent or temporary?
2. How to handle photos with intentionally wrong metadata?
3. What's the maximum group size for performance?
4. Should there be a "trusted source" designation?
5. How to handle copyright/ownership of transferred metadata?
