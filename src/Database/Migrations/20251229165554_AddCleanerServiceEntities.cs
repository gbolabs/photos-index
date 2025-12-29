using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCleanerServiceEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchivePath",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "IndexedFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "IndexedFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CleanerJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DryRun = table.Column<bool>(type: "boolean", nullable: false),
                    TotalFiles = table.Column<int>(type: "integer", nullable: false),
                    ProcessedFiles = table.Column<int>(type: "integer", nullable: false),
                    SucceededFiles = table.Column<int>(type: "integer", nullable: false),
                    FailedFiles = table.Column<int>(type: "integer", nullable: false),
                    SkippedFiles = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanerJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CleanerJobFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CleanerJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ArchivePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CleanerJobFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CleanerJobFiles_CleanerJobs_CleanerJobId",
                        column: x => x.CleanerJobId,
                        principalTable: "CleanerJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CleanerJobFiles_IndexedFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CleanerJobFiles_CleanerJobId",
                table: "CleanerJobFiles",
                column: "CleanerJobId");

            migrationBuilder.CreateIndex(
                name: "IX_CleanerJobFiles_FileId",
                table: "CleanerJobFiles",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_CleanerJobFiles_Status",
                table: "CleanerJobFiles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CleanerJobs_CreatedAt",
                table: "CleanerJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CleanerJobs_Status",
                table: "CleanerJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CleanerJobFiles");

            migrationBuilder.DropTable(
                name: "CleanerJobs");

            migrationBuilder.DropColumn(
                name: "ArchivePath",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "IndexedFiles");
        }
    }
}
