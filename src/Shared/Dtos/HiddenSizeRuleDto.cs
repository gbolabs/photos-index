namespace Shared.Dtos;

/// <summary>
/// DTO for a size-based file hiding rule.
/// </summary>
public record HiddenSizeRuleDto
{
    public Guid Id { get; init; }
    public int MaxWidth { get; init; }
    public int MaxHeight { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public int AffectedFileCount { get; init; }
}

/// <summary>
/// Request to create a new size-based hiding rule.
/// </summary>
public record CreateHiddenSizeRuleRequest
{
    /// <summary>
    /// Maximum width in pixels. Common icon sizes: 16, 32, 48, 64, 128, 256.
    /// </summary>
    public int MaxWidth { get; init; } = 256;

    /// <summary>
    /// Maximum height in pixels.
    /// </summary>
    public int MaxHeight { get; init; } = 256;

    /// <summary>
    /// Optional description for this rule.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Preview result for files that would be hidden by a size rule.
/// </summary>
public record SizeRulePreviewDto
{
    public int TotalFiles { get; init; }
    public long TotalSizeBytes { get; init; }
    public IReadOnlyList<SizeGroupDto> SizeGroups { get; init; } = [];
}

/// <summary>
/// Group of files by size dimensions.
/// </summary>
public record SizeGroupDto
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int FileCount { get; init; }
    public long TotalSizeBytes { get; init; }
}
