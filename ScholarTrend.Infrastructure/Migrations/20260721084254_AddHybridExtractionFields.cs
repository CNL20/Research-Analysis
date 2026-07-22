using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHybridExtractionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AbstractConfidence",
                table: "PaperAnalyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConclusionConfidence",
                table: "PaperAnalyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DiscussionConfidence",
                table: "PaperAnalyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HybridMetadataJson",
                table: "PaperAnalyses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsedAbstract",
                table: "PaperAnalyses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UsedConclusion",
                table: "PaperAnalyses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UsedDiscussion",
                table: "PaperAnalyses",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbstractConfidence",
                table: "PaperAnalyses");

            migrationBuilder.DropColumn(
                name: "ConclusionConfidence",
                table: "PaperAnalyses");

            migrationBuilder.DropColumn(
                name: "DiscussionConfidence",
                table: "PaperAnalyses");

            migrationBuilder.DropColumn(
                name: "HybridMetadataJson",
                table: "PaperAnalyses");

            migrationBuilder.DropColumn(
                name: "UsedAbstract",
                table: "PaperAnalyses");

            migrationBuilder.DropColumn(
                name: "UsedConclusion",
                table: "PaperAnalyses");

            migrationBuilder.DropColumn(
                name: "UsedDiscussion",
                table: "PaperAnalyses");
        }
    }
}
