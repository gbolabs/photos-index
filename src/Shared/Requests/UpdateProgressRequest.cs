using System.ComponentModel.DataAnnotations;

namespace Shared.Requests;

/// <summary>
/// Request to update indexing progress.
/// </summary>
public record UpdateProgressRequest
{
    [Range(0, int.MaxValue, ErrorMessage = "FilesScanned must be a non-negative value")]
    public int FilesScanned { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "FilesIngested must be a non-negative value")]
    public int FilesIngested { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "FilesFailed must be a non-negative value")]
    public int FilesFailed { get; init; }
}
