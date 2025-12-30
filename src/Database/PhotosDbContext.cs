using Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Database;

public class PhotosDbContext : DbContext
{
    public PhotosDbContext(DbContextOptions<PhotosDbContext> options) : base(options)
    {
    }

    public DbSet<IndexedFile> IndexedFiles { get; set; }
    public DbSet<ScanDirectory> ScanDirectories { get; set; }
    public DbSet<DuplicateGroup> DuplicateGroups { get; set; }
    public DbSet<SelectionPreference> SelectionPreferences { get; set; }
    public DbSet<CleanerJob> CleanerJobs { get; set; }
    public DbSet<CleanerJobFile> CleanerJobFiles { get; set; }
    public DbSet<HiddenFolder> HiddenFolders { get; set; }
    public DbSet<HiddenSizeRule> HiddenSizeRules { get; set; }
    public DbSet<SelectionSession> SelectionSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure IndexedFile entity
        modelBuilder.Entity<IndexedFile>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FilePath)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.FileHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(e => e.ThumbnailPath)
                .HasMaxLength(1000);

            // Indexes
            entity.HasIndex(e => e.FilePath);
            entity.HasIndex(e => e.FileHash);
            entity.HasIndex(e => e.IsDuplicate);

            // Indexes for hidden files
            entity.HasIndex(e => e.IsHidden);

            // Foreign key relationships
            entity.HasOne(e => e.DuplicateGroup)
                .WithMany(d => d.Files)
                .HasForeignKey(e => e.DuplicateGroupId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.HiddenByFolder)
                .WithMany(h => h.HiddenFiles)
                .HasForeignKey(e => e.HiddenByFolderId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.HiddenBySizeRule)
                .WithMany(h => h.HiddenFiles)
                .HasForeignKey(e => e.HiddenBySizeRuleId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure HiddenCategory as string
            entity.Property(e => e.HiddenCategory)
                .HasConversion<string>()
                .HasMaxLength(20);
        });

        // Configure ScanDirectory entity
        modelBuilder.Entity<ScanDirectory>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Path)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.IsEnabled)
                .HasDefaultValue(true);

            entity.Property(e => e.FileCount)
                .HasDefaultValue(0);

            // Indexes
            entity.HasIndex(e => e.Path);
        });

        // Configure DuplicateGroup entity
        modelBuilder.Entity<DuplicateGroup>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Hash)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(Enums.DuplicateGroupStatus.Pending);

            // Indexes
            entity.HasIndex(e => e.Hash);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ReviewOrder);
            entity.HasIndex(e => e.ReviewSessionId);

            // Relationship with SelectionSession
            entity.HasOne(e => e.ReviewSession)
                .WithMany(s => s.ReviewedGroups)
                .HasForeignKey(e => e.ReviewSessionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure SelectionSession entity
        modelBuilder.Entity<SelectionSession>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("active");

            // Indexes
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure SelectionPreference entity
        modelBuilder.Entity<SelectionPreference>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PathPrefix)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Priority)
                .HasDefaultValue(50);

            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0);

            // Indexes
            entity.HasIndex(e => e.PathPrefix);
            entity.HasIndex(e => e.SortOrder);
        });

        // Configure CleanerJob entity
        modelBuilder.Entity<CleanerJob>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.Category)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(2000);

            // Indexes
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure CleanerJobFile entity
        modelBuilder.Entity<CleanerJobFile>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FilePath)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.FileHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.ArchivePath)
                .HasMaxLength(1000);

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(2000);

            // Foreign key relationships
            entity.HasOne(e => e.CleanerJob)
                .WithMany(j => j.Files)
                .HasForeignKey(e => e.CleanerJobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.IndexedFile)
                .WithMany()
                .HasForeignKey(e => e.FileId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            entity.HasIndex(e => e.CleanerJobId);
            entity.HasIndex(e => e.FileId);
            entity.HasIndex(e => e.Status);
        });

        // Configure HiddenFolder entity
        modelBuilder.Entity<HiddenFolder>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FolderPath)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            // Indexes
            entity.HasIndex(e => e.FolderPath);
        });

        // Configure HiddenSizeRule entity
        modelBuilder.Entity<HiddenSizeRule>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            // Indexes
            entity.HasIndex(e => new { e.MaxWidth, e.MaxHeight });
        });
    }
}
