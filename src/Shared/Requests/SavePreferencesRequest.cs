using Shared.Dtos;

namespace Shared.Requests;

/// <summary>
/// Request to save selection preferences.
/// </summary>
public record SavePreferencesRequest
{
    /// <summary>
    /// Path priority preferences to save.
    /// </summary>
    public required IReadOnlyList<SelectionPreferenceDto> Preferences { get; init; }
}
