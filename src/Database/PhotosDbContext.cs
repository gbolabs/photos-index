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

            // Foreign key relationship
            entity.HasOne(e => e.DuplicateGroup)
                .WithMany(d => d.Files)
                .HasForeignKey(e => e.DuplicateGroupId)
                .OnDelete(DeleteBehavior.SetNull);
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
                .HasMaxLength(20)
                .HasDefaultValue("pending");

            // Indexes
            entity.HasIndex(e => e.Hash);
            entity.HasIndex(e => e.Status);
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
    }
}
