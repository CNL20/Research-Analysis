using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperSourcesAndDropExternalColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Drop the unique index before we can mutate the columns
            migrationBuilder.DropIndex(
                name: "IX_ResearchPaper_ExternalId",
                table: "ResearchPapers");

            // 2) Create the PaperSources table first so we can backfill data
            migrationBuilder.CreateTable(
                name: "PaperSources",
                columns: table => new
                {
                    PaperId = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceDoi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceCitationCount = table.Column<int>(type: "integer", nullable: true),
                    SourceYear = table.Column<int>(type: "integer", nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    RawMetadataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperSources", x => new { x.PaperId, x.SourceName });
                    table.ForeignKey(
                        name: "FK_PaperSources_ResearchPapers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "ResearchPapers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaperSources_ExternalId",
                table: "PaperSources",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_PaperSources_SourceDoi",
                table: "PaperSources",
                column: "SourceDoi");

            migrationBuilder.CreateIndex(
                name: "IX_PaperSources_SourceName",
                table: "PaperSources",
                column: "SourceName");

            // 3) Backfill existing data from the soon-to-be-dropped columns
            migrationBuilder.Sql(@"
                INSERT INTO ""PaperSources""
                    (""PaperId"", ""SourceName"", ""ExternalId"",
                     ""SourceDoi"", ""SourceUrl"", ""SourceCitationCount"",
                     ""FetchedAt"", ""LastSeenAt"")
                SELECT ""Id"",
                       COALESCE(""ExternalSource"", 'Unknown'),
                       COALESCE(""ExternalId"", ""Id""::text),
                       ""Doi"",
                       ""Url"",
                       ""CitationCount"",
                       ""CreatedAt"",
                       COALESCE(""UpdatedAt"", ""CreatedAt"")
                FROM ""ResearchPapers""
                WHERE ""ExternalSource"" IS NOT NULL
                  AND ""ExternalId"" IS NOT NULL
                ON CONFLICT DO NOTHING;
            ");

            // 4) Now drop the legacy columns
            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "ResearchPapers");

            migrationBuilder.DropColumn(
                name: "ExternalSource",
                table: "ResearchPapers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "ResearchPapers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSource",
                table: "ResearchPapers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""ResearchPapers"" rp
                SET ""ExternalSource"" = ps.""SourceName"",
                    ""ExternalId""    = ps.""ExternalId""
                FROM ""PaperSources"" ps
                WHERE ps.""PaperId"" = rp.""Id""
                  AND ps.""SourceName"" = (
                      SELECT ""SourceName"" FROM ""PaperSources""
                      WHERE ""PaperId"" = rp.""Id""
                      ORDER BY ""FetchedAt"" ASC
                      LIMIT 1
                  );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchPaper_ExternalId",
                table: "ResearchPapers",
                column: "ExternalId",
                unique: true);

            migrationBuilder.DropTable(
                name: "PaperSources");
        }
    }
}