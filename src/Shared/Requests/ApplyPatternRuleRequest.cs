using System.Text.Json.Serialization;

namespace Shared.Requests;

/// <summary>
/// Request to apply a selection rule to all duplicate groups matching a directory pattern.
/// </summary>
public record ApplyPatternRuleRequest
{
    /// <summary>
    /// The sorted list of directories that define the pattern to match.
    /// </summary>
    public required IReadOnlyList<string> Directories { get; init; }

    /// <summary>
    /// The directory path to prefer when selecting the original file.
    /// Files from this directory will be selected as original.
    /// </summary>
    public required string PreferredDirectory { get; init; }

    /// <summary>
    /// How to handle cases where multiple files exist in the preferred directory.
    /// </summary>
    public PatternTieBreaker TieBreaker { get; init; } = PatternTieBreaker.EarliestDate;

    /// <summary>
    /// If true, only return a preview of what would be updated without making changes.
    /// </summary>
    public bool Preview { get; init; } = false;
}

/// <summary>
/// Strategy for selecting among multiple files in the preferred directory.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PatternTieBreaker>))]
public enum PatternTieBreaker
{
    /// <summary>
    /// Select the file with the earliest creation date.
    /// </summary>
    [JsonPropertyName("earliestDate")]
    EarliestDate,

    /// <summary>
    /// Select the file with the shortest path.
    /// </summary>
    [JsonPropertyName("shortestPath")]
    ShortestPath,

    /// <summary>
    /// Select the largest file by size.
    /// </summary>
    [JsonPropertyName("largestFile")]
    LargestFile,

    /// <summary>
    /// Select the first file found (by IndexedAt).
    /// </summary>
    [JsonPropertyName("firstIndexed")]
    FirstIndexed
}
