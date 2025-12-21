using System.Reflection;
using Shared.Dtos;

namespace Api.Services;

/// <summary>
/// Service for retrieving build and version information.
/// </summary>
public interface IBuildInfoService
{
    BuildInfoDto GetBuildInfo();
    void LogStartupInfo(ILogger logger);
}

/// <summary>
/// Implementation of build info service that reads from environment variables and assembly.
/// </summary>
public class BuildInfoService : IBuildInfoService
{
    private readonly string _serviceName;
    private readonly string _version;
    private readonly string? _commitHash;
    private readonly string? _branch;
    private readonly string? _buildTime;
    private readonly string _runtimeVersion;
    private readonly string? _environment;
    private readonly DateTime _startTimeUtc;

    public BuildInfoService(IConfiguration configuration, IWebHostEnvironment env)
    {
        _serviceName = "photos-index-api";
        _startTimeUtc = DateTime.UtcNow;

        // Get version from assembly
        var assembly = Assembly.GetExecutingAssembly();
        _version = assembly.GetName().Version?.ToString() ?? "1.0.0";

        // Get build info from environment variables (set during Docker build)
        _commitHash = Environment.GetEnvironmentVariable("BUILD_COMMIT_HASH")
            ?? configuration["BuildInfo:CommitHash"]
            ?? "dev";

        _branch = Environment.GetEnvironmentVariable("BUILD_BRANCH")
            ?? configuration["BuildInfo:Branch"]
            ?? "local";

        _buildTime = Environment.GetEnvironmentVariable("BUILD_TIME")
            ?? configuration["BuildInfo:BuildTime"]
            ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        _runtimeVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        _environment = env.EnvironmentName;
    }

    public BuildInfoDto GetBuildInfo()
    {
        var uptime = DateTime.UtcNow - _startTimeUtc;

        return new BuildInfoDto
        {
            ServiceName = _serviceName,
            Version = _version,
            CommitHash = _commitHash,
            Branch = _branch,
            BuildTime = _buildTime,
            RuntimeVersion = _runtimeVersion,
            Environment = _environment,
            StartTimeUtc = _startTimeUtc,
            Uptime = FormatUptime(uptime)
        };
    }

    public void LogStartupInfo(ILogger logger)
    {
        logger.LogInformation(
            "Starting {ServiceName} v{Version} (commit: {CommitHash}, branch: {Branch}, built: {BuildTime})",
            _serviceName,
            _version,
            _commitHash,
            _branch,
            _buildTime);

        logger.LogInformation(
            "Runtime: {RuntimeVersion}, Environment: {Environment}",
            _runtimeVersion,
            _environment);
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s";
        if (uptime.TotalMinutes >= 1)
            return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
        return $"{uptime.Seconds}s";
    }
}
