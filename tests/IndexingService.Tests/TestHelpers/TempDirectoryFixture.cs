namespace IndexingService.Tests.TestHelpers;

/// <summary>
/// Creates a temporary directory for testing that is cleaned up after the test.
/// </summary>
public sealed class TempDirectoryFixture : IDisposable
{
    public string RootPath { get; }

    public TempDirectoryFixture()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"photos-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(RootPath);
    }

    public string CreateSubdirectory(string relativePath)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public string CreateFile(string relativePath, byte[]? content = null)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        content ??= "test content"u8.ToArray();
        File.WriteAllBytes(fullPath, content);
        return fullPath;
    }

    public string CreateHiddenFile(string relativePath)
    {
        var fullPath = CreateFile(relativePath);
        File.SetAttributes(fullPath, File.GetAttributes(fullPath) | FileAttributes.Hidden);
        return fullPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
