using System.ComponentModel.DataAnnotations;

namespace Shared.Requests;

/// <summary>
/// Request to create a new scan directory.
/// </summary>
public record CreateScanDirectoryRequest
{
    [Required(ErrorMessage = "Path is required")]
    [RegularExpression(@"^/.*$", ErrorMessage = "Path must be an absolute path starting with /")]
    public required string Path { get; init; }

    public bool IsEnabled { get; init; } = true;
}
