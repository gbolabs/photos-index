using System.ComponentModel.DataAnnotations;

namespace Shared.Requests;

/// <summary>
/// Request to set a file as the original in a duplicate group.
/// </summary>
public record SetOriginalRequest
{
    [Required]
    public Guid FileId { get; init; }
}
