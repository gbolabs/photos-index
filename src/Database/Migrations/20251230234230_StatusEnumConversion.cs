using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class StatusEnumConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, convert existing status values to Pascal case
            migrationBuilder.Sql(@"
                UPDATE ""DuplicateGroups""
                SET ""Status"" = CASE ""Status""
                    WHEN 'pending' THEN 'Pending'
                    WHEN 'proposed' THEN 'AutoSelected'
                    WHEN 'auto-selected' THEN 'AutoSelected'
                    WHEN 'validated' THEN 'Validated'
                    WHEN 'conflict' THEN 'Pending'
                    ELSE 'Pending'
                END
                WHERE ""Status"" NOT IN ('Pending', 'AutoSelected', 'Validated', 'Cleaning', 'CleaningFailed', 'Cleaned');
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DuplicateGroups",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "CleaningCompletedAt",
                table: "DuplicateGroups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CleaningStartedAt",
                table: "DuplicateGroups",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CleaningCompletedAt",
                table: "DuplicateGroups");

            migrationBuilder.DropColumn(
                name: "CleaningStartedAt",
                table: "DuplicateGroups");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DuplicateGroups",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "pending",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Pending");
        }
    }
}
