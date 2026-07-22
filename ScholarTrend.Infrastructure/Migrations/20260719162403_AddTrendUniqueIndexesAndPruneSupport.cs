using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrendUniqueIndexesAndPruneSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove duplicate trend rows before unique indexes are created (keep highest Id).
            migrationBuilder.Sql("""
                DELETE FROM "KeywordTrends" a
                USING "KeywordTrends" b
                WHERE a."KeywordId" = b."KeywordId"
                  AND a."Year" = b."Year"
                  AND a."Month" = b."Month"
                  AND a."Id" < b."Id";

                DELETE FROM "TopicTrends" a
                USING "TopicTrends" b
                WHERE a."TopicId" = b."TopicId"
                  AND a."Year" = b."Year"
                  AND a."Month" = b."Month"
                  AND a."Id" < b."Id";

                DELETE FROM "JournalTrends" a
                USING "JournalTrends" b
                WHERE a."JournalId" = b."JournalId"
                  AND a."Year" = b."Year"
                  AND a."Month" = b."Month"
                  AND a."Id" < b."Id";
                """);

            migrationBuilder.DropIndex(
                name: "IX_TopicTrends_TopicId",
                table: "TopicTrends");

            migrationBuilder.DropIndex(
                name: "IX_KeywordTrends_KeywordId",
                table: "KeywordTrends");

            migrationBuilder.DropIndex(
                name: "IX_JournalTrends_JournalId",
                table: "JournalTrends");

            migrationBuilder.CreateIndex(
                name: "IX_TopicTrends_TopicId_Year_Month",
                table: "TopicTrends",
                columns: new[] { "TopicId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KeywordTrends_KeywordId_Year_Month",
                table: "KeywordTrends",
                columns: new[] { "KeywordId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalTrends_JournalId_Year_Month",
                table: "JournalTrends",
                columns: new[] { "JournalId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TopicTrends_TopicId_Year_Month",
                table: "TopicTrends");

            migrationBuilder.DropIndex(
                name: "IX_KeywordTrends_KeywordId_Year_Month",
                table: "KeywordTrends");

            migrationBuilder.DropIndex(
                name: "IX_JournalTrends_JournalId_Year_Month",
                table: "JournalTrends");

            migrationBuilder.CreateIndex(
                name: "IX_TopicTrends_TopicId",
                table: "TopicTrends",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_KeywordTrends_KeywordId",
                table: "KeywordTrends",
                column: "KeywordId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalTrends_JournalId",
                table: "JournalTrends",
                column: "JournalId");
        }
    }
}
