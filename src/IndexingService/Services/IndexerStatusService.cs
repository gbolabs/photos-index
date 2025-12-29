using System.Reflection;
using Shared.Dtos;

namespace IndexingService.Services;

/// <summary>
/// Tracks the current status of this indexer instance.
/// </summary>
public interface IIndexerStatusService
{
    IndexerStatusDto GetStatus();
    void SetState(IndexerState state);
    void SetCurrentDirectory(string? directory);
    void SetActivity(string? activity);
    void SetProgress(int filesProcessed, int filesTotal);
    void SetBytesProgress(long bytesProcessed, long bytesTotal);
    void IncrementFilesProcessed(long bytesProcessed = 0);
    void RecordError(string error);
    void ClearError();
    void ScanStarted();
    void ScanCompleted();
    void SetScanQueue(IReadOnlyList<ScanQueueItemDto> queue);
    bool IsPaused { get; }
    void Pause();
    void Resume();
    CancellationToken CancellationToken { get; }
    void RequestCancellation();
    void ResetCancellation();
}

public class IndexerStatusService : IIndexerStatusService
{
    private readonly object _lock = new();
    private readonly string _indexerId;
    private readonly string _hostname;
    private readonly string _version;
    private readonly string _commitHash;
    private readonly string _environment;
    private readonly DateTime _startTime;

    private IndexerState _state = IndexerState.Idle;
    private IndexerState _stateBeforePause = IndexerState.Idle;
    private string? _currentDirectory;
    private string? _currentActivity;
    private int _filesProcessed;
    private int _filesTotal;
    private int _errorCount;
    private DateTime? _lastScanStarted;
    private DateTime? _lastScanCompleted;
    private DateTime _lastHeartbeat;
    private string? _lastError;

    // Speed tracking
    private long _bytesProcessed;
    private long _bytesTotal;
    private DateTime _speedCalculationStart;
    private int _filesAtSpeedStart;
    private long _bytesAtSpeedStart;
    private double _filesPerSecond;
    private double _bytesPerSecond;

    // Queue
    private IReadOnlyList<ScanQueueItemDto> _scanQueue = [];

    // Pause/Cancel
    private bool _isPaused;
    private CancellationTokenSource _cts = new();

    public bool IsPaused => _isPaused;
    public CancellationToken CancellationToken => _cts.Token;

    public IndexerStatusService()
    {
        _hostname = Environment.MachineName;
        _indexerId = _hostname;
        _version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
        _commitHash = Environment.GetEnvironmentVariable("BUILD_COMMIT_HASH") ?? "dev";
        _environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        _startTime = DateTime.UtcNow;
        _lastHeartbeat = DateTime.UtcNow;
        _speedCalculationStart = DateTime.UtcNow;
    }

    public IndexerStatusDto GetStatus()
    {
        lock (_lock)
        {
            _lastHeartbeat = DateTime.UtcNow;
            UpdateSpeedMetrics();

            // Calculate ETA
            int? estimatedSecondsRemaining = null;
            if (_filesPerSecond > 0 && _filesTotal > _filesProcessed)
            {
                var filesRemaining = _filesTotal - _filesProcessed;
                estimatedSecondsRemaining = (int)(filesRemaining / _filesPerSecond);
            }

            // Calculate progress percentage
            var progressPercentage = _filesTotal > 0
                ? (double)_filesProcessed / _filesTotal * 100
                : 0;

            return new IndexerStatusDto
            {
                IndexerId = _indexerId,
                Hostname = _hostname,
                Version = _version,
                CommitHash = _commitHash,
                Environment = _environment,
                State = _state,
                CurrentDirectory = _currentDirectory,
                CurrentActivity = _currentActivity,
                FilesProcessed = _filesProcessed,
                FilesTotal = _filesTotal,
                ErrorCount = _errorCount,
                LastScanStarted = _lastScanStarted,
                LastScanCompleted = _lastScanCompleted,
                ConnectedAt = _startTime,
                LastHeartbeat = _lastHeartbeat,
                Uptime = DateTime.UtcNow - _startTime,
                LastError = _lastError,
                FilesPerSecond = Math.Round(_filesPerSecond, 2),
                BytesProcessed = _bytesProcessed,
                BytesTotal = _bytesTotal,
                BytesPerSecond = Math.Round(_bytesPerSecond, 0),
                EstimatedSecondsRemaining = estimatedSecondsRemaining,
                ProgressPercentage = Math.Round(progressPercentage, 1),
                ScanQueue = _scanQueue,
                QueuedDirectories = _scanQueue.Count
            };
        }
    }

    private void UpdateSpeedMetrics()
    {
        var elapsed = (DateTime.UtcNow - _speedCalculationStart).TotalSeconds;
        if (elapsed >= 1) // Update speed every second
        {
            var filesProcessedSinceStart = _filesProcessed - _filesAtSpeedStart;
            var bytesProcessedSinceStart = _bytesProcessed - _bytesAtSpeedStart;

            _filesPerSecond = filesProcessedSinceStart / elapsed;
            _bytesPerSecond = bytesProcessedSinceStart / elapsed;

            // Reset for next calculation window (rolling average over 5 seconds)
            if (elapsed >= 5)
            {
                _speedCalculationStart = DateTime.UtcNow;
                _filesAtSpeedStart = _filesProcessed;
                _bytesAtSpeedStart = _bytesProcessed;
            }
        }
    }

    public void SetState(IndexerState state)
    {
        lock (_lock)
        {
            _state = state;
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    public void SetCurrentDirectory(string? directory)
    {
        lock (_lock)
        {
            _currentDirectory = directory;
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    public void SetActivity(string? activity)
    {
        lock (_lock)
        {
            _currentActivity = activity;
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    public void SetProgress(int filesProcessed, int filesTotal)
    {
        lock (_lock)
        {
            _filesProcessed = filesProcessed;
            _filesTotal = filesTotal;
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    public void IncrementFilesProcessed(long bytesProcessed = 0)
    {
        lock (_lock)
        {
            _filesProcessed++;
            _bytesProcessed += bytesProcessed;
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    public void SetBytesProgress(long bytesProcessed, long bytesTotal)
    {
        lock (_lock)
        {
            _bytesProcessed = bytesProcessed;
            _bytesTotal = bytesTotal;
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    public void RecordError(string error)
    {
        lock (_lock)
        {
            _errorCount++;
            _lastError = error;
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    public void ClearError()
    {
        lock (_lock)
        {
            _lastError = null;
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    public void ScanStarted()
    {
        lock (_lock)
        {
            _state = IndexerState.Scanning;
            _lastScanStarted = DateTime.UtcNow;
            _filesProcessed = 0;
            _filesTotal = 0;
            _bytesProcessed = 0;
            _bytesTotal = 0;
            _filesPerSecond = 0;
            _bytesPerSecond = 0;
            _speedCalculationStart = DateTime.UtcNow;
            _filesAtSpeedStart = 0;
            _bytesAtSpeedStart = 0;
            _isPaused = false;
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    public void ScanCompleted()
    {
        lock (_lock)
        {
            _state = IndexerState.Idle;
            _lastScanCompleted = DateTime.UtcNow;
            _currentDirectory = null;
            _scanQueue = [];
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    public void SetScanQueue(IReadOnlyList<ScanQueueItemDto> queue)
    {
        lock (_lock)
        {
            _scanQueue = queue;
            _lastHeartbeat = DateTime.UtcNow;
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_state != IndexerState.Idle && _state != IndexerState.Paused)
            {
                _stateBeforePause = _state;
                _state = IndexerState.Paused;
                _isPaused = true;
                _lastHeartbeat = DateTime.UtcNow;
            }
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_state == IndexerState.Paused)
            {
                _state = _stateBeforePause;
                _isPaused = false;
                // Reset speed calculation after pause
                _speedCalculationStart = DateTime.UtcNow;
                _filesAtSpeedStart = _filesProcessed;
                _bytesAtSpeedStart = _bytesProcessed;
                _lastHeartbeat = DateTime.UtcNow;
            }
        }
    }

    public void RequestCancellation()
    {
        _cts.Cancel();
    }

    public void ResetCancellation()
    {
        if (_cts.IsCancellationRequested)
        {
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }
    }
}
