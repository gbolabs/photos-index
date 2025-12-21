namespace IndexingService.Models;

public class IndexingOptions
{
    public bool GenerateThumbnails { get; set; } = false; // Disabled by default for large collections
    public int BatchSize { get; set; } = 100;
    public int MaxParallelism { get; set; } = 4;
}
