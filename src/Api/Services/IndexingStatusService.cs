using Shared.Dtos;

namespace Api.Services;

/// <summary>
/// Thread-safe service for managing indexing status and progress.
/// </summary>
public class IndexingStatusService : IIndexingStatusService
{
    private readonly object _lock = new();
    private IndexingStatusDto _status = new()
    {
        IsRunning = false,
        FilesScanned = 0,
        FilesIngested = 0,
        FilesFailed = 0
    };

    public IndexingStatusDto GetStatus()
    {
        lock (_lock)
        {
            return _status;
        }
    }

    public void StartIndexing(Guid directoryId, string directoryPath)
    {
        lock (_lock)
        {
            _status = new IndexingStatusDto
            {
                IsRunning = true,
                CurrentDirectoryId = directoryId,
                CurrentDirectoryPath = directoryPath,
                FilesScanned = 0,
                FilesIngested = 0,
                FilesFailed = 0,
                StartedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };
        }
    }

    public void UpdateProgress(int scanned, int ingested, int failed)
    {
        lock (_lock)
        {
            _status = _status with
            {
                FilesScanned = scanned,
                FilesIngested = ingested,
                FilesFailed = failed,
                LastUpdatedAt = DateTime.UtcNow
            };
        }
    }

    public void StopIndexing()
    {
        lock (_lock)
        {
            _status = _status with
            {
                IsRunning = false,
                LastUpdatedAt = DateTime.UtcNow
            };
        }
    }
}
