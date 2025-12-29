namespace Shared.Dtos;

/// <summary>
/// Result of a duplicate scan operation.
/// </summary>
public class DuplicateScanResultDto
{
    /// <summary>
    /// Total files scanned for duplicates.
    /// </summary>
    public int TotalFilesScanned { get; set; }

    /// <summary>
    /// Number of new duplicate groups created.
    /// </summary>
    public int NewGroupsCreated { get; set; }

    /// <summary>
    /// Number of existing groups updated.
    /// </summary>
    public int GroupsUpdated { get; set; }

    /// <summary>
    /// Total duplicate groups after scan.
    /// </summary>
    public int TotalGroups { get; set; }

    /// <summary>
    /// Total duplicate files found.
    /// </summary>
    public int TotalDuplicateFiles { get; set; }

    /// <summary>
    /// Potential space savings in bytes.
    /// </summary>
    public long PotentialSavingsBytes { get; set; }

    /// <summary>
    /// Time taken to complete the scan in milliseconds.
    /// </summary>
    public long ScanDurationMs { get; set; }
}
