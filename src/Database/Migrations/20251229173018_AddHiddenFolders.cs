using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddHiddenFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenAt",
                table: "IndexedFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HiddenByFolderId",
                table: "IndexedFiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenCategory",
                table: "IndexedFiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "IndexedFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "HiddenFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiddenFolders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_HiddenByFolderId",
                table: "IndexedFiles",
                column: "HiddenByFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_IsHidden",
                table: "IndexedFiles",
                column: "IsHidden");

            migrationBuilder.CreateIndex(
                name: "IX_HiddenFolders_FolderPath",
                table: "HiddenFolders",
                column: "FolderPath");

            migrationBuilder.AddForeignKey(
                name: "FK_IndexedFiles_HiddenFolders_HiddenByFolderId",
                table: "IndexedFiles",
                column: "HiddenByFolderId",
                principalTable: "HiddenFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IndexedFiles_HiddenFolders_HiddenByFolderId",
                table: "IndexedFiles");

            migrationBuilder.DropTable(
                name: "HiddenFolders");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_HiddenByFolderId",
                table: "IndexedFiles");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_IsHidden",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "HiddenAt",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "HiddenByFolderId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "HiddenCategory",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "IndexedFiles");
        }
    }
}
