using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Shared.Dtos;

namespace CleanerService.Services;

public interface IDeleteService
{
    Task<DeleteFileResult> DeleteFileAsync(DeleteFileRequest request, CancellationToken ct);
}

public class DeleteService : IDeleteService
{
    private readonly HttpClient _httpClient;
    private readonly CleanerServiceOptions _options;
    private readonly ICleanerStatusService _statusService;
    private readonly ILogger<DeleteService> _logger;

    public DeleteService(
        HttpClient httpClient,
        IOptions<CleanerServiceOptions> options,
        ICleanerStatusService statusService,
        ILogger<DeleteService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _statusService = statusService;
        _logger = logger;
    }

    public async Task<DeleteFileResult> DeleteFileAsync(DeleteFileRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Processing delete request: Job={JobId}, File={FileId}, Path={Path}, DryRun={DryRun}",
            request.JobId, request.FileId, request.FilePath, _options.DryRunEnabled);

        _statusService.SetState(CleanerState.Processing);
        _statusService.SetCurrentJob(request.JobId);
        _statusService.SetActivity($"Processing: {Path.GetFileName(request.FilePath)}");

        try
        {
            // Step 1: Verify file exists
            if (!File.Exists(request.FilePath))
            {
                _logger.LogWarning("File not found: {Path}", request.FilePath);
                return CreateResult(request, false, "File not found on disk");
            }

            var fileInfo = new FileInfo(request.FilePath);

            // Step 2: Verify hash matches
            _statusService.SetActivity($"Verifying: {Path.GetFileName(request.FilePath)}");
            var computedHash = await ComputeHashAsync(request.FilePath, ct);

            if (!string.Equals(computedHash, request.FileHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Hash mismatch for {Path}: expected {Expected}, got {Actual}",
                    request.FilePath, request.FileHash, computedHash);
                return CreateResult(request, false, $"Hash mismatch: file has been modified");
            }

            // Step 3: Upload file to API archive
            _statusService.SetState(CleanerState.Uploading);
            _statusService.SetActivity($"Uploading: {Path.GetFileName(request.FilePath)}");

            var archivePath = await UploadToArchiveAsync(request, ct);
            if (archivePath == null)
            {
                return CreateResult(request, false, "Failed to upload to archive");
            }

            _statusService.AddBytesArchived(fileInfo.Length);

            // Step 4: Delete file (if not dry-run)
            if (_options.DryRunEnabled)
            {
                _logger.LogInformation("DRY-RUN: Would delete {Path}", request.FilePath);
                _statusService.IncrementFilesProcessed();
                _statusService.IncrementFilesSkipped();

                return CreateResult(request, true, null, archivePath, wasDryRun: true);
            }

            _statusService.SetState(CleanerState.Deleting);
            _statusService.SetActivity($"Deleting: {Path.GetFileName(request.FilePath)}");

            File.Delete(request.FilePath);
            _logger.LogInformation("Deleted file: {Path}", request.FilePath);

            _statusService.IncrementFilesProcessed();
            _statusService.IncrementFilesDeleted();

            // Step 5: Confirm deletion to API
            await ConfirmDeleteAsync(request, archivePath, ct);

            return CreateResult(request, true, null, archivePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing delete for {Path}", request.FilePath);
            _statusService.RecordError(ex.Message);
            _statusService.IncrementFilesFailed();

            return CreateResult(request, false, ex.Message);
        }
        finally
        {
            _statusService.SetState(CleanerState.Idle);
            _statusService.SetActivity(null);
        }
    }

    private async Task<string> ComputeHashAsync(string filePath, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private async Task<string?> UploadToArchiveAsync(DeleteFileRequest request, CancellationToken ct)
    {
        try
        {
            await using var stream = File.OpenRead(request.FilePath);

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(request.JobId.ToString()), "jobId");
            content.Add(new StringContent(request.FileId.ToString()), "fileId");
            content.Add(new StringContent(request.Category.ToString()), "category");
            content.Add(new StringContent(request.FilePath), "originalPath");
            content.Add(new StringContent(request.FileHash), "fileHash");

            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(request.FilePath));
            content.Add(fileContent, "file", Path.GetFileName(request.FilePath));

            var response = await _httpClient.PostAsync("/api/cleaner/archive", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Archive upload failed: {StatusCode} - {Error}", response.StatusCode, error);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<ArchiveResult>(ct);
            return result?.ArchivePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file to archive");
            return null;
        }
    }

    private async Task ConfirmDeleteAsync(DeleteFileRequest request, string archivePath, CancellationToken ct)
    {
        try
        {
            var result = new DeleteFileResult
            {
                JobId = request.JobId,
                FileId = request.FileId,
                Success = true,
                ArchivePath = archivePath,
                WasDryRun = _options.DryRunEnabled
            };

            var response = await _httpClient.PostAsJsonAsync("/api/cleaner/confirm-delete", result, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to confirm delete: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to confirm delete to API");
        }
    }

    private DeleteFileResult CreateResult(
        DeleteFileRequest request,
        bool success,
        string? error,
        string? archivePath = null,
        bool wasDryRun = false)
    {
        return new DeleteFileResult
        {
            JobId = request.JobId,
            FileId = request.FileId,
            Success = success,
            Error = error,
            ArchivePath = archivePath,
            WasDryRun = wasDryRun || _options.DryRunEnabled
        };
    }

    private static string GetContentType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            _ => "application/octet-stream"
        };
    }

    private record ArchiveResult(bool Success, string? ArchivePath, string? Error);
}
