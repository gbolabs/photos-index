# ADR-013: Cleaner Service Architecture for Safe Duplicate Removal

**Status**: Accepted
**Date**: 2025-12-29
**Author**: Claude Code

## Context

Photos Index identifies duplicate files across mounted volumes. When users decide to delete duplicates, files must be safely removed with:
- **Backup/archive capability** before permanent deletion
- **Retention policies** based on deletion category
- **Dry-run mode** for safe testing
- **Transaction logging** for audit and rollback

### Deployment Architecture

The system runs in a distributed environment:
- **Synology NAS**: Source of truth for photos (IndexingService with RO access)
- **TrueNAS**: API, database, services (MinIO for archives)

**Key constraint**: Only a service running on Synology can delete files from Synology volumes.

### Problem

We need a service that can:
1. Receive delete commands from the API
2. Archive files to TrueNAS before deletion
3. Safely delete files from Synology
4. Report status and progress to UI
5. Support bulk operations (100+ files at once)
6. Apply retention policies to archived files

## Decision

Implement a **CleanerService** that runs on Synology with RW access and communicates with the API via SignalR:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              TrueNAS                                         │
│  ┌─────────┐    ┌───────────┐    ┌────────────────┐    ┌───────────────┐   │
│  │   UI    │───▶│    API    │───▶│ CleanerHub     │    │ RetentionJob  │   │
│  │(Angular)│    │           │    │ (SignalR)      │    │ (Background)  │   │
│  └─────────┘    └───────────┘    └───────┬────────┘    └───────────────┘   │
│                       │                   │                    │            │
│                       ▼                   │                    ▼            │
│               ┌───────────────┐           │            ┌───────────────┐   │
│               │ CleanerJob    │           │            │ MinIO Archive │   │
│               │ (Background)  │           │            │ /archive/     │   │
│               └───────────────┘           │            └───────────────┘   │
│                                           │                                 │
└───────────────────────────────────────────│─────────────────────────────────┘
                                            │
                                     SignalR│WebSocket
                                            │
┌───────────────────────────────────────────│─────────────────────────────────┐
│                              Synology     │                                 │
│                              ┌────────────▼────────────┐                    │
│  ┌─────────────┐            │    CleanerService       │                    │
│  │ IndexService│ (RO)       │    (SignalR Client)     │ (RW)               │
│  │             │            │                         │                    │
│  └─────────────┘            │  ┌───────────────────┐  │                    │
│                              │  │ DeleteFile(path)  │  │                    │
│                              │  │ 1. Read file      │  │                    │
│  ┌─────────────────────┐    │  │ 2. Upload to API  │  │                    │
│  │  Photo Volumes      │    │  │ 3. On 2xx: delete │  │                    │
│  │  /volume1/Photos    │◀───│  └───────────────────┘  │                    │
│  │  /volume1/photo     │    │                         │                    │
│  │  /volume1/Public    │    └─────────────────────────┘                    │
│  └─────────────────────┘                                                    │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Communication Flow

#### Delete Request (User → API → CleanerService)

```
1. User: Selects duplicates in UI, clicks "Delete"
2. UI: POST /api/cleaner/delete { fileIds: [...], dryRun: false }
3. API: Creates CleanerJob, queues files for deletion
4. API: SignalR "DeleteFile(jobId, fileId, path, hash)"
5. CleanerService: Receives command
6. CleanerService: Reads file from disk
7. CleanerService: POST /api/cleaner/archive (multipart upload)
8. API: Stores file in MinIO archive bucket
9. API: Returns 2xx
10. CleanerService: Deletes file from Synology
11. CleanerService: SignalR "ReportDeleteComplete(jobId, fileId, success)"
12. API: Updates database (marks file as deleted)
13. API: Deletes thumbnail from MinIO thumbnails bucket
14. UI: Receives progress updates via SignalR
```

### SignalR Hub Contract

```csharp
// Commands from API to CleanerService
public interface ICleanerClient
{
    Task DeleteFile(Guid jobId, Guid fileId, string filePath, string expectedHash);
    Task DeleteFiles(Guid jobId, IEnumerable<DeleteFileRequest> files);
    Task CancelJob(Guid jobId);
    Task SetDryRun(bool enabled);
}

// Events from CleanerService to API/UI
public interface ICleanerHub
{
    Task ReportStatus(CleanerStatusDto status);
    Task ReportDeleteProgress(Guid jobId, Guid fileId, string status);
    Task ReportDeleteComplete(Guid jobId, Guid fileId, bool success, string? error);
    Task ReportJobComplete(Guid jobId, int succeeded, int failed, int skipped);
}
```

### Archive Storage Categories

Files are stored in MinIO with category-based paths:

| Category | Path Pattern | Retention | Use Case |
|----------|--------------|-----------|----------|
| `hash_duplicate` | `/archive/hash_duplicate/YYYY-MM/` | 6 months | Exact hash duplicates |
| `near_duplicate` | `/archive/near_duplicate/YYYY-MM/` | 2 years | Perceptual/visual duplicates |
| `manual` | `/archive/manual/YYYY-MM/` | 2 years | User-initiated deletions |

Archive file naming: `{original_filename}_{fileId}_{hash}.{ext}`

### Retention Background Job

A daily job runs on TrueNAS to clean up expired archives:

```csharp
public class RetentionBackgroundService : BackgroundService
{
    // Runs daily at 3 AM
    // Scans archive buckets for files older than retention period
    // Permanently deletes expired files
    // Logs all deletions for audit
}
```

### Cleaner Job Entity

```csharp
public class CleanerJob
{
    public Guid Id { get; set; }
    public CleanerJobStatus Status { get; set; }
    public DeleteCategory Category { get; set; }
    public bool DryRun { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SucceededFiles { get; set; }
    public int FailedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public ICollection<CleanerJobFile> Files { get; set; }
}

public class CleanerJobFile
{
    public Guid Id { get; set; }
    public Guid CleanerJobId { get; set; }
    public Guid FileId { get; set; }
    public string FilePath { get; set; }
    public string FileHash { get; set; }
    public CleanerFileStatus Status { get; set; }
    public string? ArchivePath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
```

### Dry-Run Mode

CleanerService starts with `DRY_RUN=true` by default:
- File is read and verified
- Upload to API is performed (file is archived)
- **File is NOT deleted from disk**
- Progress reported as "dry-run: would delete"

Set `DRY_RUN=false` in environment to enable actual deletions.

### Configuration

#### CleanerService (Synology)

```yaml
environment:
  API_BASE_URL: https://tn.isago.ch:8053
  DRY_RUN: "true"  # Safety: disable actual deletions by default
volumes:
  - /volume1/Public:/public:rw
  - /volume1/photo:/photo:rw
  - /volume1/Photos:/photos:rw
```

#### API (TrueNAS)

```yaml
environment:
  Cleaner__ArchiveBucket: archive
  Cleaner__RetentionDays__HashDuplicate: 180
  Cleaner__RetentionDays__NearDuplicate: 730
  Cleaner__RetentionDays__Manual: 730
  Cleaner__RetentionCheckTime: "03:00"  # 3 AM
```

## Consequences

### Positive

- **Safety first**: Dry-run mode by default prevents accidental deletions
- **Audit trail**: All deletions logged with full context
- **Recovery window**: Archives allow file recovery within retention period
- **Consistent pattern**: Follows IndexerService SignalR architecture
- **Bulk operations**: Handles 100+ files efficiently via job queue
- **Category-based retention**: Different retention for different deletion types
- **No Synology .trash dependency**: We control the archive lifecycle
- **Thumbnail cleanup**: Thumbnails deleted from MinIO when file is removed

### Negative

- **Complexity**: Additional service and hub to maintain
- **Storage cost**: Archives consume TrueNAS storage until retention expires
- **Network dependency**: Requires reliable network between Synology and TrueNAS
- **Upload overhead**: Each file must be uploaded before deletion

### Neutral

- **Volume mounts differ**: CleanerService needs RW, IndexerService stays RO
- **Two services on Synology**: Indexer and Cleaner run separately
- **Archive bucket**: New MinIO bucket required for archives

## Implementation Files

| Component | Files |
|-----------|-------|
| API Hub | `src/Api/Hubs/CleanerHub.cs` |
| API Controller | `src/Api/Controllers/CleanerController.cs` |
| API Background Job | `src/Api/Services/CleanerBackgroundService.cs`, `RetentionBackgroundService.cs` |
| Database | `src/Database/Entities/CleanerJob.cs`, `CleanerJobFile.cs` |
| Shared DTOs | `src/Shared/Dtos/CleanerStatusDto.cs`, `CleanerJobDto.cs` |
| CleanerService | `src/CleanerService/Services/SignalRClientService.cs`, `DeleteService.cs` |
| Deploy | `deploy/nas/synology-0.4.5.yml`, `deploy/nas/truenas-0.4.5.yml` |

## References

- ADR-008: SignalR for API-Indexer Communication
- Backlog: `docs/backlog/04-cleaner-service/001-delete-manager.md`
