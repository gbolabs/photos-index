using Database;
using Database.Entities;

namespace Integration.Tests.Fixtures;

/// <summary>
/// Helper class for seeding test data in integration tests.
/// </summary>
public static class TestDataSeeder
{
    /// <summary>
    /// Seeds a scan directory in the database.
    /// </summary>
    public static async Task<ScanDirectory> SeedDirectoryAsync(
        PhotosDbContext db,
        string path = "/photos/test",
        bool isEnabled = true)
    {
        var directory = new ScanDirectory
        {
            Id = Guid.NewGuid(),
            Path = path,
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow,
            FileCount = 0
        };
        db.ScanDirectories.Add(directory);
        await db.SaveChangesAsync();
        return directory;
    }

    /// <summary>
    /// Seeds an indexed file in the database.
    /// </summary>
    public static async Task<IndexedFile> SeedFileAsync(
        PhotosDbContext db,
        string fileName = "test.jpg",
        string? hash = null,
        long fileSize = 1024)
    {
        var file = new IndexedFile
        {
            Id = Guid.NewGuid(),
            FilePath = $"/photos/{fileName}",
            FileName = fileName,
            FileHash = hash ?? Guid.NewGuid().ToString("N"),
            FileSize = fileSize,
            ModifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IndexedAt = DateTime.UtcNow,
            IsDuplicate = false
        };
        db.IndexedFiles.Add(file);
        await db.SaveChangesAsync();
        return file;
    }

    /// <summary>
    /// Seeds a duplicate group with multiple files.
    /// </summary>
    public static async Task<DuplicateGroup> SeedDuplicateGroupAsync(
        PhotosDbContext db,
        int fileCount = 3,
        long fileSize = 2048)
    {
        var hash = Guid.NewGuid().ToString("N");
        var group = new DuplicateGroup
        {
            Id = Guid.NewGuid(),
            Hash = hash,
            FileCount = fileCount,
            TotalSize = fileSize * fileCount,
            CreatedAt = DateTime.UtcNow
        };
        db.DuplicateGroups.Add(group);

        for (int i = 0; i < fileCount; i++)
        {
            var file = new IndexedFile
            {
                Id = Guid.NewGuid(),
                FilePath = $"/photos/dup{i}.jpg",
                FileName = $"dup{i}.jpg",
                FileHash = hash,
                FileSize = fileSize,
                DuplicateGroupId = group.Id,
                IsDuplicate = true,
                ModifiedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IndexedAt = DateTime.UtcNow
            };
            db.IndexedFiles.Add(file);
        }

        await db.SaveChangesAsync();
        return group;
    }

    /// <summary>
    /// Clears all data from the database.
    /// </summary>
    public static async Task ClearAllDataAsync(PhotosDbContext db)
    {
        db.IndexedFiles.RemoveRange(db.IndexedFiles);
        db.DuplicateGroups.RemoveRange(db.DuplicateGroups);
        db.ScanDirectories.RemoveRange(db.ScanDirectories);
        await db.SaveChangesAsync();
    }
}
