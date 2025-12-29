namespace Shared.Dtos;

/// <summary>
/// Data transfer object for hidden folder information.
/// </summary>
public record HiddenFolderDto
{
    public Guid Id { get; init; }
    public required string FolderPath { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public int AffectedFileCount { get; init; }
}

/// <summary>
/// Request to create a new hidden folder rule.
/// </summary>
public record CreateHiddenFolderRequest
{
    public required string FolderPath { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Request to hide specific files.
/// </summary>
public record HideFilesRequest
{
    public required List<Guid> FileIds { get; init; }
}

/// <summary>
/// DTO for folder path autocomplete.
/// </summary>
public record FolderPathDto
{
    public required string Path { get; init; }
    public int FileCount { get; init; }
}
