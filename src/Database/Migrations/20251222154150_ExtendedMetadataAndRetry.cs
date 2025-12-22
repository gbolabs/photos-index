using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class ExtendedMetadataAndRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Aperture",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CameraMake",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CameraModel",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateTaken",
                table: "IndexedFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GpsLatitude",
                table: "IndexedFiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GpsLongitude",
                table: "IndexedFiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Iso",
                table: "IndexedFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "IndexedFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ShutterSpeed",
                table: "IndexedFiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aperture",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "CameraMake",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "CameraModel",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "DateTaken",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "GpsLatitude",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "GpsLongitude",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Iso",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "ShutterSpeed",
                table: "IndexedFiles");
        }
    }
}
