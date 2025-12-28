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
            //
            // Uses batched updates (10000 rows at a time) to avoid timeout on large tables
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    batch_size INT := 10000;
                    rows_updated INT;
                BEGIN
                    LOOP
                        UPDATE "IndexedFiles"
                        SET "ThumbnailProcessedAt" = "IndexedAt"
                        WHERE "Id" IN (
                            SELECT "Id" FROM "IndexedFiles"
                            WHERE "ThumbnailPath" IS NOT NULL
                              AND "ThumbnailProcessedAt" IS NULL
                            LIMIT batch_size
                        );
                        GET DIAGNOSTICS rows_updated = ROW_COUNT;
                        EXIT WHEN rows_updated = 0;
                        RAISE NOTICE 'Updated % rows for ThumbnailProcessedAt', rows_updated;
                    END LOOP;
                END $$;
                """, suppressTransaction: true);

            // Also backfill MetadataProcessedAt for files that have metadata
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    batch_size INT := 10000;
                    rows_updated INT;
                BEGIN
                    LOOP
                        UPDATE "IndexedFiles"
                        SET "MetadataProcessedAt" = "IndexedAt"
                        WHERE "Id" IN (
                            SELECT "Id" FROM "IndexedFiles"
                            WHERE "CameraMake" IS NOT NULL
                              AND "MetadataProcessedAt" IS NULL
                            LIMIT batch_size
                        );
                        GET DIAGNOSTICS rows_updated = ROW_COUNT;
                        EXIT WHEN rows_updated = 0;
                        RAISE NOTICE 'Updated % rows for MetadataProcessedAt', rows_updated;
                    END LOOP;
                END $$;
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No rollback needed - the NULL values were incorrect anyway
        }
    }
}
