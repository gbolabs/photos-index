using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddHiddenSizeRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HiddenBySizeRuleId",
                table: "IndexedFiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HiddenSizeRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxWidth = table.Column<int>(type: "integer", nullable: false),
                    MaxHeight = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiddenSizeRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_HiddenBySizeRuleId",
                table: "IndexedFiles",
                column: "HiddenBySizeRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_HiddenSizeRules_MaxWidth_MaxHeight",
                table: "HiddenSizeRules",
                columns: new[] { "MaxWidth", "MaxHeight" });

            migrationBuilder.AddForeignKey(
                name: "FK_IndexedFiles_HiddenSizeRules_HiddenBySizeRuleId",
                table: "IndexedFiles",
                column: "HiddenBySizeRuleId",
                principalTable: "HiddenSizeRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IndexedFiles_HiddenSizeRules_HiddenBySizeRuleId",
                table: "IndexedFiles");

            migrationBuilder.DropTable(
                name: "HiddenSizeRules");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_HiddenBySizeRuleId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "HiddenBySizeRuleId",
                table: "IndexedFiles");
        }
    }
}
