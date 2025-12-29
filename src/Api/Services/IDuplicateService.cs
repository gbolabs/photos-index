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

    // Pattern rule methods
    Task<DirectoryPatternDto?> GetPatternForGroupAsync(Guid groupId, CancellationToken ct);
    Task<ApplyPatternRuleResultDto> ApplyPatternRuleAsync(ApplyPatternRuleRequest request, CancellationToken ct);

    // Navigation methods
    Task<GroupNavigationDto> GetNavigationAsync(Guid groupId, string? statusFilter, CancellationToken ct);

    // Session methods for keyboard-driven review
    Task<SelectionSessionDto> StartOrResumeSessionAsync(bool resumeExisting, CancellationToken ct);
    Task<SelectionSessionDto?> GetCurrentSessionAsync(CancellationToken ct);
    Task<SelectionSessionDto> PauseSessionAsync(Guid sessionId, CancellationToken ct);
    Task<ReviewActionResultDto> ProposeOriginalAsync(Guid groupId, Guid fileId, CancellationToken ct);
    Task<ReviewActionResultDto> ValidateGroupAsync(Guid groupId, CancellationToken ct);
    Task<ReviewActionResultDto> SkipGroupAsync(Guid groupId, CancellationToken ct);
    Task<ReviewActionResultDto> UndoLastActionAsync(Guid groupId, CancellationToken ct);
    Task<SessionProgressDto> GetSessionProgressAsync(Guid sessionId, CancellationToken ct);
}
