using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace IndexingService.ApiClient;

/// <summary>
/// HTTP client for communicating with the Photos Index API.
/// </summary>
public class PhotosApiClient : IPhotosApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PhotosApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private static readonly ActivitySource ActivitySource = new("PhotosIndex.IndexingService.ApiClient");

    public PhotosApiClient(HttpClient httpClient, ILogger<PhotosApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScanDirectoryDto>> GetScanDirectoriesAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("GetScanDirectories");

        try
        {
            _logger.LogDebug("Fetching all scan directories from API");

            var response = await _httpClient.GetAsync("api/scandirectories", cancellationToken);
            response.EnsureSuccessStatusCode();

            var directories = await response.Content.ReadFromJsonAsync<List<ScanDirectoryDto>>(_jsonOptions, cancellationToken);

            _logger.LogInformation("Retrieved {Count} scan directories from API", directories?.Count ?? 0);
            activity?.SetTag("directory.count", directories?.Count ?? 0);

            return directories ?? new List<ScanDirectoryDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching scan directories");
            activity?.SetTag("error", true);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching scan directories");
            activity?.SetTag("error", true);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScanDirectoryDto>> GetEnabledScanDirectoriesAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("GetEnabledScanDirectories");

        try
        {
            _logger.LogDebug("Fetching enabled scan directories from API");

            var allDirectories = await GetScanDirectoriesAsync(cancellationToken);
            var enabledDirectories = allDirectories.Where(d => d.IsEnabled).ToList();

            _logger.LogInformation("Retrieved {Count} enabled scan directories from API", enabledDirectories.Count);
            activity?.SetTag("directory.enabled_count", enabledDirectories.Count);

            return enabledDirectories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching enabled scan directories");
            activity?.SetTag("error", true);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<BatchOperationResponse> BatchIngestFilesAsync(
        BatchIngestFilesRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("BatchIngestFiles");
        activity?.SetTag("directory.id", request.ScanDirectoryId);
        activity?.SetTag("files.count", request.Files.Count);

        try
        {
            _logger.LogDebug(
                "Batch ingesting {Count} files for directory {DirectoryId}",
                request.Files.Count,
                request.ScanDirectoryId);

            var response = await _httpClient.PostAsJsonAsync(
                "api/files/batch",
                request,
                _jsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BatchOperationResponse>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("API returned null response");

            _logger.LogInformation(
                "Batch ingest completed: {Success} succeeded, {Failed} failed for directory {DirectoryId}",
                result.Succeeded,
                result.Failed,
                request.ScanDirectoryId);

            activity?.SetTag("result.success_count", result.Succeeded);
            activity?.SetTag("result.failure_count", result.Failed);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error while batch ingesting {Count} files for directory {DirectoryId}",
                request.Files.Count,
                request.ScanDirectoryId);
            activity?.SetTag("error", true);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while batch ingesting {Count} files for directory {DirectoryId}",
                request.Files.Count,
                request.ScanDirectoryId);
            activity?.SetTag("error", true);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateLastScannedAsync(Guid directoryId, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("UpdateLastScanned");
        activity?.SetTag("directory.id", directoryId);

        try
        {
            _logger.LogDebug("Updating last scanned timestamp for directory {DirectoryId}", directoryId);

            var response = await _httpClient.PatchAsync(
                $"api/scandirectories/{directoryId}/last-scanned",
                null,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Updated last scanned timestamp for directory {DirectoryId}", directoryId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error while updating last scanned timestamp for directory {DirectoryId}",
                directoryId);
            activity?.SetTag("error", true);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while updating last scanned timestamp for directory {DirectoryId}",
                directoryId);
            activity?.SetTag("error", true);
            throw;
        }
    }
}
