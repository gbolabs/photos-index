namespace Shared.Dtos;

/// <summary>
/// Data transfer object for selection preference configuration.
/// </summary>
public record SelectionPreferenceDto
{
    public Guid Id { get; init; }
    public required string PathPrefix { get; init; }
    public int Priority { get; init; }
    public int SortOrder { get; init; }
}
