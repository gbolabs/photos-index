using System.ComponentModel.DataAnnotations;

namespace Shared.Requests;

/// <summary>
/// Request to update an existing scan directory.
/// </summary>
public record UpdateScanDirectoryRequest
{
    [RegularExpression(@"^/.*$", ErrorMessage = "Path must be an absolute path starting with /")]
    public string? Path { get; init; }

    public bool? IsEnabled { get; init; }
}
