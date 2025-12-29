using Shared.Dtos;

namespace Api.Services;

/// <summary>
/// Service for managing hidden folder rules.
/// </summary>
public interface IHiddenFolderService
{
    /// <summary>
    /// Gets all hidden folder rules with affected file counts.
    /// </summary>
    Task<IReadOnlyList<HiddenFolderDto>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Gets distinct folder paths for autocomplete, optionally filtered by search term.
    /// </summary>
    Task<IReadOnlyList<FolderPathDto>> GetFolderPathsAsync(string? search, CancellationToken ct);

    /// <summary>
    /// Creates a new hidden folder rule and marks matching files as hidden.
    /// </summary>
    Task<HiddenFolderDto> CreateAsync(CreateHiddenFolderRequest request, CancellationToken ct);

    /// <summary>
    /// Deletes a hidden folder rule and unhides files that were hidden by it.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Hides specific files manually.
    /// </summary>
    Task<int> HideFilesAsync(HideFilesRequest request, CancellationToken ct);

    /// <summary>
    /// Unhides specific files.
    /// </summary>
    Task<int> UnhideFilesAsync(HideFilesRequest request, CancellationToken ct);

    /// <summary>
    /// Gets the count of hidden files.
    /// </summary>
    Task<int> GetHiddenCountAsync(CancellationToken ct);

    // Size rule methods

    /// <summary>
    /// Gets all size rules with affected file counts.
    /// </summary>
    Task<IReadOnlyList<HiddenSizeRuleDto>> GetAllSizeRulesAsync(CancellationToken ct);

    /// <summary>
    /// Previews files that would be hidden by a size rule without applying it.
    /// </summary>
    Task<SizeRulePreviewDto> PreviewSizeRuleAsync(int maxWidth, int maxHeight, CancellationToken ct);

    /// <summary>
    /// Creates a size rule and hides matching files.
    /// </summary>
    Task<HiddenSizeRuleDto> CreateSizeRuleAsync(CreateHiddenSizeRuleRequest request, CancellationToken ct);

    /// <summary>
    /// Deletes a size rule and unhides files hidden by it.
    /// </summary>
    Task<bool> DeleteSizeRuleAsync(Guid id, CancellationToken ct);
}
