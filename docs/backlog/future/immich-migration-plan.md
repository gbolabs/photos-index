# Immich Migration Plan

**Status**: 📋 Planning
**Created**: 2025-12-28
**Priority**: High - Kids growing fast!

## Context

After building photos-index for deduplication, evaluating whether to continue full development or migrate to Immich for day-to-day photo management.

## Decision

**Hybrid approach**: Use photos-index for deduplication/cleanup, Immich for daily use.

## Why Immich

| Requirement | Immich | Photos-Index |
|-------------|--------|--------------|
| Alternative to Synology Photos | ✅ Better | ⚠️ Basic UI |
| No US cloud (privacy) | ✅ Self-hosted | ✅ Self-hosted |
| iOS app with auto-backup | ✅ Excellent | ❌ None |
| ML/face recognition | ✅ Local ML | 🔲 Planned |
| Smart search | ✅ CLIP-based | 🔲 Planned |
| Deduplication | ⚠️ Basic | ✅ Advanced |
| Active community | ✅ 50K+ stars | ❌ Solo project |

## Photos-Index Role (Keep)

1. **Pre-import deduplication** - Clean library before Immich import
2. **Periodic maintenance** - Find new duplicates over time
3. **Hash-based verification** - Ensure no data loss during migration
4. **iCloud import prep** - Deduplicate iCloud export before Immich

## Features to Evaluate in Immich

### Must Have
- [ ] iOS app quality (test with family)
- [ ] External library support (point to existing NAS folders)
- [ ] Face recognition accuracy
- [ ] Search quality (find "kids at beach")

### Nice to Have (for digital frames / memories)
- [ ] **Memories "On this day"** - ✅ Built-in
- [ ] **Shared albums** - ✅ Built-in with public links
- [ ] **Digital frame support** - Check [Immich Kiosk](https://github.com/damongolding/immich-kiosk)
- [ ] **Year-in-review** - ❌ Not built-in, but could use API + custom script
- [ ] **Album proposals** - ❌ Manual, but could build with photos-index AI

### Custom Features to Build (if needed)
- [ ] **Year-in-review generator** - Use Immich API to select best photos
- [ ] **Smart album proposals** - Leverage photos-index perceptual hashing + AI
- [ ] **Digital frame slideshow** - Immich Kiosk or custom integration

## Migration Steps

### Phase 1: Cleanup with Photos-Index
1. [ ] Complete full scan of all NAS directories
2. [ ] Run duplicate detection (hash-based)
3. [ ] Review and clean duplicates via UI
4. [ ] Export clean file list

### Phase 2: Immich Setup on TrueNAS
1. [ ] Install Immich via Docker/TrueCharts
2. [ ] Configure external library (read-only access to photos)
3. [ ] Run initial ML processing
4. [ ] Test iOS app with family

### Phase 3: Daily Use
1. [ ] Configure iOS auto-backup to Immich
2. [ ] Set up shared albums for family
3. [ ] Configure digital frame if applicable

### Phase 4: Maintenance
1. [ ] Periodic dedup scan with photos-index
2. [ ] iCloud export → photos-index dedup → Immich import

## Resources

- [Immich GitHub](https://github.com/immich-app/immich)
- [Immich Docs](https://immich.app/docs)
- [Immich TrueNAS install](https://immich.app/docs/install/truenas)
- [Immich Kiosk (digital frames)](https://github.com/damongolding/immich-kiosk)
- [Immich Frame discussion](https://github.com/immich-app/immich/discussions/1531)

## Open Questions

1. Can Immich coexist with Synology Photos pointing to same library?
2. How to handle HEIC from iCloud?
3. Best approach for 40+ years of family photos (incremental import?)
4. Year-in-review: build custom or wait for Immich feature?

## Photos-Index Features to Keep/Develop

Even with Immich, these photos-index features remain valuable:

1. **Advanced deduplication**
   - Perceptual hashing (near-duplicates, crops, resizes)
   - Burst detection
   - Cross-source matching (iCloud ↔ local)

2. **Pre-processing pipeline**
   - HEIC conversion if needed
   - Metadata normalization
   - Quality scoring for duplicate selection

3. **AI-powered album proposals** (future)
   - Year-in-review generation
   - Event detection
   - Best photo selection for calendars/books
