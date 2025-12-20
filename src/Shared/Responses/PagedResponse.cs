namespace Shared.Responses;

/// <summary>
/// Generic paged response wrapper.
/// </summary>
public record PagedResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalItems / (double)PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    public static PagedResponse<T> Empty(int page = 1, int pageSize = 50) => new()
    {
        Items = [],
        Page = page,
        PageSize = pageSize,
        TotalItems = 0
    };
}
