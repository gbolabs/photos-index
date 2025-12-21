using System.ComponentModel.DataAnnotations;

namespace Shared.Requests;

/// <summary>
/// Request to start indexing for a directory.
/// </summary>
public record StartIndexingRequest
{
    [Required(ErrorMessage = "DirectoryId is required")]
    public Guid DirectoryId { get; init; }

    [Required(ErrorMessage = "DirectoryPath is required")]
    public required string DirectoryPath { get; init; }
}
