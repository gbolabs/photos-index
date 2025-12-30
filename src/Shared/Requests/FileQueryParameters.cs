using System.ComponentModel.DataAnnotations;

namespace Shared.Requests;

/// <summary>
/// Query parameters for filtering and paginating indexed files.
/// </summary>
public record FileQueryParameters
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 50;

    public Guid? DirectoryId { get; init; }

    public bool? HasDuplicates { get; init; }

    public DateTime? MinDate { get; init; }

    public DateTime? MaxDate { get; init; }

    [StringLength(200)]
    public string? Search { get; init; }

    public FileSortBy SortBy { get; init; } = FileSortBy.IndexedAt;

    public bool SortDescending { get; init; } = true;

    /// <summary>
    /// Whether to include hidden files in the results. Defaults to false.
    /// </summary>
    public bool IncludeHidden { get; init; } = false;
}

public enum FileSortBy
{
    Name,
    Size,
    CreatedAt,
    ModifiedAt,
    IndexedAt,
    DateTaken
}
