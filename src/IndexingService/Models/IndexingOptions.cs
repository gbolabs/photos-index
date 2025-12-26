namespace IndexingService.Models;

public class IndexingOptions
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5000";
    public bool GenerateThumbnails { get; set; } = false; // Disabled by default for distributed processing
    public bool ExtractMetadata { get; set; } = false; // Disabled by default for distributed processing
    public int BatchSize { get; set; } = 250; // Larger batches = fewer API round trips
    public int MaxParallelism { get; set; } = 8; // I/O bound operations can exceed core count
}
