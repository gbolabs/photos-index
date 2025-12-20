using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Services;

/// <summary>
/// Service for managing scan directories.
/// </summary>
public interface IScanDirectoryService
{
    Task<PagedResponse<ScanDirectoryDto>> GetAllAsync(int page, int pageSize, CancellationToken ct);
    Task<ScanDirectoryDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ScanDirectoryDto> CreateAsync(CreateScanDirectoryRequest request, CancellationToken ct);
    Task<ScanDirectoryDto?> UpdateAsync(Guid id, UpdateScanDirectoryRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    Task<bool> TriggerScanAsync(Guid id, CancellationToken ct);
    Task<bool> PathExistsAsync(string path, CancellationToken ct);
}
