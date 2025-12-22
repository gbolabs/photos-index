using Shared.Dtos;
using Shared.Requests;
using Shared.Responses;

namespace Api.Services;

/// <summary>
/// Service for smart selection of original files in duplicate groups.
/// </summary>
public interface IOriginalSelectionService
{
    /// <summary>
    /// Get the current selection configuration.
    /// </summary>
    Task<SelectionConfigDto> GetConfigAsync(CancellationToken ct);

    /// <summary>
    /// Get all path priority preferences.
    /// </summary>
    Task<IReadOnlyList<SelectionPreferenceDto>> GetPreferencesAsync(CancellationToken ct);

    /// <summary>
    /// Save path priority preferences.
    /// </summary>
    Task SavePreferencesAsync(SavePreferencesRequest request, CancellationToken ct);

    /// <summary>
    /// Reset preferences to default values.
    /// </summary>
    Task ResetToDefaultsAsync(CancellationToken ct);

    /// <summary>
    /// Recalculate original selections for duplicate groups.
    /// </summary>
    Task<RecalculateOriginalsResponse> RecalculateOriginalsAsync(RecalculateOriginalsRequest request, CancellationToken ct);

    /// <summary>
    /// Calculate score for a single file based on current configuration.
    /// </summary>
    Task<int> CalculateFileScoreAsync(Guid fileId, CancellationToken ct);
}
