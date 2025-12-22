using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDuplicateGroupValidationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "KeptFileId",
                table: "DuplicateGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "DuplicateGroups",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidatedAt",
                table: "DuplicateGroups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateGroups_Status",
                table: "DuplicateGroups",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DuplicateGroups_Status",
                table: "DuplicateGroups");

            migrationBuilder.DropColumn(
                name: "KeptFileId",
                table: "DuplicateGroups");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DuplicateGroups");

            migrationBuilder.DropColumn(
                name: "ValidatedAt",
                table: "DuplicateGroups");
        }
    }
}
