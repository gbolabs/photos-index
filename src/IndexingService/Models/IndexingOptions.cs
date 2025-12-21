namespace IndexingService.Models;

public class IndexingOptions
{
    public bool GenerateThumbnails { get; set; } = false; // Disabled by default for large collections
    public int BatchSize { get; set; } = 250; // Larger batches = fewer API round trips
    public int MaxParallelism { get; set; } = 8; // I/O bound operations can exceed core count
}
