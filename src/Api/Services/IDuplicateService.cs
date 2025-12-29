using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Services;

/// <summary>
/// Service for managing duplicate file groups.
/// </summary>
public interface IDuplicateService
{
    Task<PagedResponse<DuplicateGroupDto>> GetGroupsAsync(int page, int pageSize, string? statusFilter, CancellationToken ct);
    Task<DuplicateGroupDto?> GetGroupAsync(Guid id, CancellationToken ct);
    Task<bool> SetOriginalAsync(Guid groupId, Guid fileId, CancellationToken ct);
    Task<Guid?> AutoSelectOriginalAsync(Guid groupId, AutoSelectRequest request, CancellationToken ct);
    Task<int> AutoSelectAllAsync(AutoSelectRequest request, CancellationToken ct);
    Task<FileStatisticsDto> GetStatisticsAsync(CancellationToken ct);
    Task<int> QueueNonOriginalsForDeletionAsync(Guid groupId, CancellationToken ct);

    // Validation methods
    Task<int> ValidateDuplicatesAsync(ValidateDuplicatesRequest request, CancellationToken ct);
    Task<ValidateBatchResponse> ValidateBatchAsync(ValidateBatchRequest request, CancellationToken ct);
    Task<int> UndoValidationAsync(UndoValidationRequest request, CancellationToken ct);

    // Scan methods
    Task<DuplicateScanResultDto> ScanForDuplicatesAsync(CancellationToken ct);
}
