using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Services;

/// <summary>
/// Service for managing duplicate file groups.
/// </summary>
public interface IDuplicateService
{
    Task<PagedResponse<DuplicateGroupDto>> GetGroupsAsync(int page, int pageSize, CancellationToken ct);
    Task<DuplicateGroupDto?> GetGroupAsync(Guid id, CancellationToken ct);
    Task<bool> SetOriginalAsync(Guid groupId, Guid fileId, CancellationToken ct);
    Task<Guid?> AutoSelectOriginalAsync(Guid groupId, AutoSelectRequest request, CancellationToken ct);
    Task<int> AutoSelectAllAsync(AutoSelectRequest request, CancellationToken ct);
    Task<FileStatisticsDto> GetStatisticsAsync(CancellationToken ct);
    Task<int> QueueNonOriginalsForDeletionAsync(Guid groupId, CancellationToken ct);
}
