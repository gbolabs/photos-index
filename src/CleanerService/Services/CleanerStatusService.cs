using System.Reflection;
using Microsoft.Extensions.Options;
using Shared.Dtos;

namespace CleanerService.Services;

public interface ICleanerStatusService
{
    CleanerStatusDto GetStatus();
    void SetState(CleanerState state);
    void SetActivity(string? activity);
    void SetCurrentJob(Guid? jobId);
    void IncrementFilesProcessed();
    void IncrementFilesDeleted();
    void IncrementFilesFailed();
    void IncrementFilesSkipped();
    void AddBytesArchived(long bytes);
    void RecordError(string error);
    void Reset();
}

public class CleanerStatusService : ICleanerStatusService
{
    private readonly CleanerServiceOptions _options;
    private readonly string _hostname;
    private readonly string? _version;
    private readonly string? _commitHash;
    private readonly DateTime _startedAt;

    private CleanerState _state = CleanerState.Idle;
    private string? _currentActivity;
    private Guid? _currentJobId;
    private int _filesProcessed;
    private int _filesDeleted;
    private int _filesFailed;
    private int _filesSkipped;
    private long _bytesArchived;
    private int _errorCount;
    private string? _lastError;
    private DateTime? _lastJobStarted;
    private DateTime? _lastJobCompleted;
    private DateTime _lastHeartbeat;

    private readonly object _lock = new();

    public CleanerStatusService(IOptions<CleanerServiceOptions> options)
    {
        _options = options.Value;
        _hostname = Environment.MachineName;
        _startedAt = DateTime.UtcNow;
        _lastHeartbeat = DateTime.UtcNow;

        // Get version info from assembly
        var assembly = Assembly.GetExecutingAssembly();
        _version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        _commitHash = _version?.Split('+').LastOrDefault();
    }

    public CleanerStatusDto GetStatus()
    {
        lock (_lock)
        {
            _lastHeartbeat = DateTime.UtcNow;

            return new CleanerStatusDto
            {
                CleanerId = _hostname,
                Hostname = _hostname,
                Version = _version,
                CommitHash = _commitHash,
                Environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production",
                State = _state,
                DryRunEnabled = _options.DryRunEnabled,
                CurrentActivity = _currentActivity,
                CurrentJobId = _currentJobId,
                FilesProcessed = _filesProcessed,
                FilesDeleted = _filesDeleted,
                FilesFailed = _filesFailed,
                FilesSkipped = _filesSkipped,
                BytesArchived = _bytesArchived,
                ErrorCount = _errorCount,
                LastError = _lastError,
                LastJobStarted = _lastJobStarted,
                LastJobCompleted = _lastJobCompleted,
                ConnectedAt = _startedAt,
                LastHeartbeat = _lastHeartbeat,
                Uptime = DateTime.UtcNow - _startedAt
            };
        }
    }

    public void SetState(CleanerState state)
    {
        lock (_lock)
        {
            _state = state;
            if (state == CleanerState.Processing)
            {
                _lastJobStarted = DateTime.UtcNow;
            }
            else if (state == CleanerState.Idle && _lastJobStarted.HasValue)
            {
                _lastJobCompleted = DateTime.UtcNow;
            }
        }
    }

    public void SetActivity(string? activity)
    {
        lock (_lock)
        {
            _currentActivity = activity;
        }
    }

    public void SetCurrentJob(Guid? jobId)
    {
        lock (_lock)
        {
            _currentJobId = jobId;
        }
    }

    public void IncrementFilesProcessed()
    {
        lock (_lock)
        {
            _filesProcessed++;
        }
    }

    public void IncrementFilesDeleted()
    {
        lock (_lock)
        {
            _filesDeleted++;
        }
    }

    public void IncrementFilesFailed()
    {
        lock (_lock)
        {
            _filesFailed++;
        }
    }

    public void IncrementFilesSkipped()
    {
        lock (_lock)
        {
            _filesSkipped++;
        }
    }

    public void AddBytesArchived(long bytes)
    {
        lock (_lock)
        {
            _bytesArchived += bytes;
        }
    }

    public void RecordError(string error)
    {
        lock (_lock)
        {
            _errorCount++;
            _lastError = error;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _filesProcessed = 0;
            _filesDeleted = 0;
            _filesFailed = 0;
            _filesSkipped = 0;
            _bytesArchived = 0;
            _currentJobId = null;
            _currentActivity = null;
        }
    }
}
