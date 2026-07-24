using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ResearchPaper_Title",
                table: "ResearchPapers");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_TopicTrends_TrendingScore",
                table: "TopicTrends",
                column: "TrendingScore");

            migrationBuilder.CreateIndex(
                name: "IX_TopicTrends_Year_Month",
                table: "TopicTrends",
                columns: new[] { "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_ResearchPaper_Title",
                table: "ResearchPapers",
                column: "Title")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_KeywordTrends_TrendingScore",
                table: "KeywordTrends",
                column: "TrendingScore");

            migrationBuilder.CreateIndex(
                name: "IX_KeywordTrends_Year_Month",
                table: "KeywordTrends",
                columns: new[] { "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalTrends_TrendingScore",
                table: "JournalTrends",
                column: "TrendingScore");

            migrationBuilder.CreateIndex(
                name: "IX_JournalTrends_Year_Month",
                table: "JournalTrends",
                columns: new[] { "Year", "Month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TopicTrends_TrendingScore",
                table: "TopicTrends");

            migrationBuilder.DropIndex(
                name: "IX_TopicTrends_Year_Month",
                table: "TopicTrends");

            migrationBuilder.DropIndex(
                name: "IX_ResearchPaper_Title",
                table: "ResearchPapers");

            migrationBuilder.DropIndex(
                name: "IX_KeywordTrends_TrendingScore",
                table: "KeywordTrends");

            migrationBuilder.DropIndex(
                name: "IX_KeywordTrends_Year_Month",
                table: "KeywordTrends");

            migrationBuilder.DropIndex(
                name: "IX_JournalTrends_TrendingScore",
                table: "JournalTrends");

            migrationBuilder.DropIndex(
                name: "IX_JournalTrends_Year_Month",
                table: "JournalTrends");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchPaper_Title",
                table: "ResearchPapers",
                column: "Title");
        }
    }
}
