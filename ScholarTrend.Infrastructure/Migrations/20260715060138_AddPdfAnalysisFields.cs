using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfAnalysisFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalysisError",
                table: "PaperPdfFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisResultJson",
                table: "PaperPdfFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisStatus",
                table: "PaperPdfFiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExtractedAt",
                table: "PaperPdfFiles",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "PaperPdfFiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalysisError",
                table: "PaperPdfFiles");

            migrationBuilder.DropColumn(
                name: "AnalysisResultJson",
                table: "PaperPdfFiles");

            migrationBuilder.DropColumn(
                name: "AnalysisStatus",
                table: "PaperPdfFiles");

            migrationBuilder.DropColumn(
                name: "ExtractedAt",
                table: "PaperPdfFiles");

            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "PaperPdfFiles");
        }
    }
}
