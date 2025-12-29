using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSelectionSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReviewedAt",
                table: "DuplicateGroups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewOrder",
                table: "DuplicateGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewSessionId",
                table: "DuplicateGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SelectionSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    TotalGroups = table.Column<int>(type: "integer", nullable: false),
                    GroupsProposed = table.Column<int>(type: "integer", nullable: false),
                    GroupsValidated = table.Column<int>(type: "integer", nullable: false),
                    GroupsSkipped = table.Column<int>(type: "integer", nullable: false),
                    CurrentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastReviewedGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelectionSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateGroups_ReviewOrder",
                table: "DuplicateGroups",
                column: "ReviewOrder");

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateGroups_ReviewSessionId",
                table: "DuplicateGroups",
                column: "ReviewSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SelectionSessions_CreatedAt",
                table: "SelectionSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SelectionSessions_Status",
                table: "SelectionSessions",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_DuplicateGroups_SelectionSessions_ReviewSessionId",
                table: "DuplicateGroups",
                column: "ReviewSessionId",
                principalTable: "SelectionSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DuplicateGroups_SelectionSessions_ReviewSessionId",
                table: "DuplicateGroups");

            migrationBuilder.DropTable(
                name: "SelectionSessions");

            migrationBuilder.DropIndex(
                name: "IX_DuplicateGroups_ReviewOrder",
                table: "DuplicateGroups");

            migrationBuilder.DropIndex(
                name: "IX_DuplicateGroups_ReviewSessionId",
                table: "DuplicateGroups");

            migrationBuilder.DropColumn(
                name: "LastReviewedAt",
                table: "DuplicateGroups");

            migrationBuilder.DropColumn(
                name: "ReviewOrder",
                table: "DuplicateGroups");

            migrationBuilder.DropColumn(
                name: "ReviewSessionId",
                table: "DuplicateGroups");
        }
    }
}
