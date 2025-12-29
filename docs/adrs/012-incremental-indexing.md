# ADR-012: Incremental Indexing with Scan Sessions

**Status**: Accepted
**Date**: 2025-12-29
**Author**: Claude Code (Opus 4.5)

## Context

The IndexingService runs periodically (every 5 minutes by default) to scan directories for new or modified photos. When Watchtower updates container images, the indexer restarts and previously would re-scan all files from the beginning, even if they hadn't changed. This caused:

1. **Unnecessary network traffic** - Files would be re-uploaded to the API even when unchanged
2. **Wasted processing time** - Hash computation and metadata extraction for unchanged files
3. **Increased load** on both the indexer and API services
4. **Longer time to detect new files** - Busy re-processing old files

## Decision

Implement a two-tier incremental indexing system:

### Tier 1: Database-Backed File Change Detection (Persisted)

The API provides an endpoint `POST /api/files/needs-reindex` that checks file modification timestamps against the database. This comparison is:
- **Persisted** - survives indexer restarts
- **Reliable** - uses database as source of truth
- **Batch-oriented** - checks multiple files in one request

**Request format:**
```json
{
  "directoryId": "guid",
  "files": [
    { "filePath": "/photos/image.jpg", "modifiedAt": "2025-12-29T10:00:00Z" }
  ]
}
```

**Response format:**
```json
[
  { "filePath": "/photos/image.jpg", "needsReindex": false }
]
```

### Tier 2: In-Memory Scan Session Tracking (Ephemeral)

Within a single indexing cycle, the `ScanSessionService` tracks:
- **Scanned directories** - which directories have been fully scanned
- **Processed files** - which files have been processed in this session
- **Hierarchical masking** - if `/photos` is scanned, `/photos/2023` is automatically covered

This provides:
- **Within-cycle optimization** - avoids duplicate processing if files appear in overlapping scans
- **Directory-level skipping** - child directories of scanned parents are automatically skipped
- **Fast in-memory checks** - no API calls needed for session-level deduplication

### How They Work Together

```
Indexing Cycle Start
        ↓
    StartNewSession() ──────────────────────────────┐
        ↓                                           │
For each directory:                                 │ Session State
    ↓                                               │ (in-memory)
    IsPathCoveredByScannedDirectory? ←──────────────┤
    Yes → Skip entirely                             │
    No  ↓                                           │
    Scan files                                      │
        ↓                                           │
    For each batch:                                 │
        ↓                                           │
        Filter out session-processed files ←────────┤
            ↓                                       │
        CheckFilesNeedReindexAsync (API) ←── Database State
            ↓                                       │
        Filter to only changed files               │
            ↓                                       │
        Compute hashes                             │
            ↓                                       │
        Ingest to API                              │
            ↓                                       │
        MarkFileProcessed() ────────────────────────┤
        ↓                                           │
    MarkDirectoryScanned() ─────────────────────────┘
```

## Session Lifecycle

| Event | Effect |
|-------|--------|
| Indexer starts | New session created |
| Cycle begins | `StartNewSession()` clears previous session data |
| Directory completes | Marked as scanned (enables hierarchical masking) |
| File ingested | Marked as processed (prevents re-processing in same session) |
| Indexer restarts | Session lost (Tier 1 provides restart resilience) |

## Persistence Model

| Data Type | Storage | Survives Restart? | Purpose |
|-----------|---------|-------------------|---------|
| File modification times | PostgreSQL (via API) | Yes | Primary change detection |
| Session directories | In-memory (`ConcurrentDictionary`) | No | Within-cycle deduplication |
| Session files | In-memory (`ConcurrentDictionary`) | No | Within-cycle deduplication |

### Why Not Persist Session Data?

1. **Simplicity** - No additional database tables or storage needed
2. **Correctness** - After restart, Tier 1 guarantees unchanged files are skipped
3. **Performance** - Memory checks are faster than database queries
4. **Statelessness** - Service can scale horizontally without shared state

## Consequences

### Positive

- **Significant bandwidth savings** - Unchanged files are never re-uploaded
- **Faster indexing cycles** - Only new/modified files are processed
- **Reduced API load** - Fewer ingest requests
- **Restart resilience** - Database-backed Tier 1 ensures correct behavior after restarts
- **Hierarchical optimization** - Directory-level masking provides additional speedup

### Negative

- **Additional API call per batch** - `CheckFilesNeedReindexAsync` adds overhead (mitigated by batching)
- **Memory usage** - Session tracking uses memory proportional to files processed
- **Session lost on restart** - Must rely on Tier 1 for restart scenarios (acceptable trade-off)

### Not Yet Implemented

1. **UI Management** - No current way to view/manage scan sessions from the web interface
2. **Session Persistence** - Sessions are purely in-memory and lost on restart
3. **Cross-indexer Coordination** - Multiple indexer instances would have separate sessions

## UI Management Considerations

Future enhancements could add:

1. **Session Status Panel** - Show current session ID, start time, and progress
2. **Force Re-scan** - Button to clear Tier 1 modification times for a directory
3. **Session History** - Log of past sessions with statistics

These would require:
- API endpoints for session status
- Database tables for session history (optional)
- Angular components for visualization

## References

- Files changed: `ScanSessionService.cs`, `IndexingOrchestrator.cs`, `PhotosApiClient.cs`
- Existing endpoint: `POST /api/files/needs-reindex` (already implemented in `IndexedFilesController`)
- Related ADR: [004-distributed-processing-architecture.md](./004-distributed-processing-architecture.md)
