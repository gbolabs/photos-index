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
    void IncrementFilesProcessed();
    void RecordError(string error);
    void ClearError();
    void ScanStarted();
    void ScanCompleted();
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
    private string? _currentDirectory;
    private string? _currentActivity;
    private int _filesProcessed;
    private int _filesTotal;
    private int _errorCount;
    private DateTime? _lastScanStarted;
    private DateTime? _lastScanCompleted;
    private DateTime _lastHeartbeat;
    private string? _lastError;

    public IndexerStatusService()
    {
        _hostname = Environment.MachineName;
        _indexerId = _hostname;
        _version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
        _commitHash = Environment.GetEnvironmentVariable("BUILD_COMMIT_HASH") ?? "dev";
        _environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        _startTime = DateTime.UtcNow;
        _lastHeartbeat = DateTime.UtcNow;
    }

    public IndexerStatusDto GetStatus()
    {
        lock (_lock)
        {
            _lastHeartbeat = DateTime.UtcNow;
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
                LastError = _lastError
            };
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

    public void IncrementFilesProcessed()
    {
        lock (_lock)
        {
            _filesProcessed++;
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
            _lastHeartbeat = DateTime.UtcNow;
        }
    }
}
