using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class BackfillThumbnailProcessedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill ThumbnailProcessedAt for files that already have thumbnails
            // This fixes the "Missing Thumbnail" counter showing wrong values
            // after the ThumbnailProcessedAt field was added in migration 20251226135951
            migrationBuilder.Sql("""
                UPDATE "IndexedFiles"
                SET "ThumbnailProcessedAt" = "IndexedAt"
                WHERE "ThumbnailPath" IS NOT NULL
                  AND "ThumbnailProcessedAt" IS NULL;
                """);

            // Also backfill MetadataProcessedAt for files that have metadata
            migrationBuilder.Sql("""
                UPDATE "IndexedFiles"
                SET "MetadataProcessedAt" = "IndexedAt"
                WHERE "CameraMake" IS NOT NULL
                  AND "MetadataProcessedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No rollback needed - the NULL values were incorrect anyway
        }
    }
}
