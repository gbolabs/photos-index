using Api.Hubs;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Dtos;

namespace Api.Controllers;

/// <summary>
/// System information and health endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly IBuildInfoService _buildInfoService;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        IBuildInfoService buildInfoService,
        ILogger<SystemController> logger)
    {
        _buildInfoService = buildInfoService;
        _logger = logger;
    }

    /// <summary>
    /// Get version information for all services in the system.
    /// </summary>
    [HttpGet("versions")]
    [ProducesResponseType(typeof(SystemVersionsDto), StatusCodes.Status200OK)]
    public ActionResult<SystemVersionsDto> GetVersions()
    {
        var apiBuildInfo = _buildInfoService.GetBuildInfo();
        var indexerStatuses = IndexerHub.GetConnectedIndexerStatuses();
        var cleanerStatuses = CleanerHub.GetConnectedCleanerStatuses();
        var thumbnailStatuses = ThumbnailServiceHub.GetConnectedServiceStatuses();
        var metadataStatuses = MetadataServiceHub.GetConnectedServiceStatuses();

        var result = new SystemVersionsDto
        {
            Api = new ServiceVersionDto
            {
                ServiceName = apiBuildInfo.ServiceName,
                Version = apiBuildInfo.Version,
                CommitHash = apiBuildInfo.CommitHash,
                Branch = apiBuildInfo.Branch,
                BuildTime = apiBuildInfo.BuildTime,
                Uptime = apiBuildInfo.Uptime,
                IsAvailable = true
            },
            Indexers = indexerStatuses.Select(s => new ServiceVersionDto
            {
                ServiceName = "photos-index-indexer",
                Version = s.Version ?? "unknown",
                CommitHash = s.CommitHash,
                InstanceId = s.IndexerId,
                Uptime = FormatUptime(s.Uptime),
                IsAvailable = s.State != IndexerState.Disconnected && s.State != IndexerState.Error
            }).ToList(),
            Cleaners = cleanerStatuses.Select(s => new ServiceVersionDto
            {
                ServiceName = "photos-index-cleaner",
                Version = s.Version ?? "unknown",
                CommitHash = s.CommitHash,
                InstanceId = s.CleanerId,
                Uptime = FormatUptime(s.Uptime),
                IsAvailable = s.State != CleanerState.Disconnected && s.State != CleanerState.Error
            }).ToList(),
            ThumbnailService = thumbnailStatuses.FirstOrDefault() is { } thumbStatus
                ? new ServiceVersionDto
                {
                    ServiceName = "photos-index-thumbnail",
                    Version = thumbStatus.Version ?? "unknown",
                    CommitHash = thumbStatus.CommitHash,
                    InstanceId = thumbStatus.ServiceId,
                    Uptime = FormatUptime(thumbStatus.Uptime),
                    IsAvailable = thumbStatus.State != ThumbnailServiceState.Disconnected && thumbStatus.State != ThumbnailServiceState.Error
                }
                : null,
            MetadataService = metadataStatuses.FirstOrDefault() is { } metaStatus
                ? new ServiceVersionDto
                {
                    ServiceName = "photos-index-metadata",
                    Version = metaStatus.Version ?? "unknown",
                    CommitHash = metaStatus.CommitHash,
                    InstanceId = metaStatus.ServiceId,
                    Uptime = FormatUptime(metaStatus.Uptime),
                    IsAvailable = metaStatus.State != MetadataServiceState.Disconnected && metaStatus.State != MetadataServiceState.Error
                }
                : null
        };

        return Ok(result);
    }

    /// <summary>
    /// Get API service build information.
    /// </summary>
    [HttpGet("info")]
    [ProducesResponseType(typeof(BuildInfoDto), StatusCodes.Status200OK)]
    public ActionResult<BuildInfoDto> GetInfo()
    {
        return Ok(_buildInfoService.GetBuildInfo());
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
