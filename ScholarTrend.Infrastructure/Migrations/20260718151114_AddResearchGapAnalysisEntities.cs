using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResearchGapAnalysisEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaperId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AnalysisType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisJobs_ResearchPapers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "ResearchPapers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoverageReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    TotalPapers = table.Column<int>(type: "integer", nullable: false),
                    PdfAnalyzedPapers = table.Column<int>(type: "integer", nullable: false),
                    AbstractAnalyzedPapers = table.Column<int>(type: "integer", nullable: false),
                    MetadataOnlyPapers = table.Column<int>(type: "integer", nullable: false),
                    IgnoredPapers = table.Column<int>(type: "integer", nullable: false),
                    CoveragePercentage = table.Column<double>(type: "double precision", nullable: false),
                    AbstractCoveragePercentage = table.Column<double>(type: "double precision", nullable: false),
                    FullTextCoveragePercentage = table.Column<double>(type: "double precision", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoverageReports_ResearchTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "ResearchTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DatasetPatterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    DatasetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PaperCount = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    GrowthRate = table.Column<double>(type: "double precision", nullable: false),
                    MinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatasetPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatasetPatterns_ResearchTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "ResearchTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GapTimelines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    GapType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GapTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PaperCount = table.Column<int>(type: "integer", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedInYear = table.Column<int>(type: "integer", nullable: true),
                    Trend = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GrowthRate = table.Column<double>(type: "double precision", nullable: false),
                    TrackedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GapTimelines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GapTimelines_ResearchTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "ResearchTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LimitationPatterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    LimitationText = table.Column<string>(type: "text", nullable: false),
                    PaperCount = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    GrowthRate = table.Column<double>(type: "double precision", nullable: false),
                    MinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LimitationPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LimitationPatterns_ResearchTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "ResearchTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MethodPatterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    MethodName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PaperCount = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    GrowthRate = table.Column<double>(type: "double precision", nullable: false),
                    MinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MethodPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MethodPatterns_ResearchTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "ResearchTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaperAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaperId = table.Column<int>(type: "integer", nullable: false),
                    ResearchProblem = table.Column<string>(type: "text", nullable: true),
                    Method = table.Column<string>(type: "text", nullable: true),
                    Dataset = table.Column<string>(type: "text", nullable: true),
                    Metric = table.Column<string>(type: "text", nullable: true),
                    Contribution = table.Column<string>(type: "text", nullable: true),
                    MethodsJson = table.Column<string>(type: "text", nullable: true),
                    DatasetsJson = table.Column<string>(type: "text", nullable: true),
                    LimitationsJson = table.Column<string>(type: "text", nullable: true),
                    FutureWorkJson = table.Column<string>(type: "text", nullable: true),
                    DiscussionsJson = table.Column<string>(type: "text", nullable: true),
                    ConclusionsJson = table.Column<string>(type: "text", nullable: true),
                    KeywordsJson = table.Column<string>(type: "text", nullable: true),
                    EvidenceSentence = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<int>(type: "integer", nullable: false),
                    AnalysisLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AnalysisSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaperAnalyses_ResearchPapers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "ResearchPapers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaperQualities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaperId = table.Column<int>(type: "integer", nullable: false),
                    HasPdf = table.Column<bool>(type: "boolean", nullable: false),
                    HasAbstract = table.Column<bool>(type: "boolean", nullable: false),
                    HasFullText = table.Column<bool>(type: "boolean", nullable: false),
                    AbstractLength = table.Column<int>(type: "integer", nullable: false),
                    AuthorCount = table.Column<int>(type: "integer", nullable: false),
                    HasDoi = table.Column<bool>(type: "boolean", nullable: false),
                    HasKeywords = table.Column<bool>(type: "boolean", nullable: false),
                    HasJournal = table.Column<bool>(type: "boolean", nullable: false),
                    CitationCount = table.Column<int>(type: "integer", nullable: false),
                    QualityScore = table.Column<int>(type: "integer", nullable: false),
                    QualityGrade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AnalysisLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperQualities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaperQualities_ResearchPapers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "ResearchPapers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResearchGaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    GapType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SuggestedDirection = table.Column<string>(type: "text", nullable: false),
                    EvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<int>(type: "integer", nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsValidated = table.Column<bool>(type: "boolean", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchGaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResearchGaps_ResearchTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "ResearchTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResearchGapEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResearchGapId = table.Column<int>(type: "integer", nullable: false),
                    PaperId = table.Column<int>(type: "integer", nullable: false),
                    EvidenceSentence = table.Column<string>(type: "text", nullable: false),
                    EvidenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SectionSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PageContext = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<int>(type: "integer", nullable: false),
                    IsValidated = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchGapEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResearchGapEvidences_ResearchGaps_ResearchGapId",
                        column: x => x.ResearchGapId,
                        principalTable: "ResearchGaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResearchGapEvidences_ResearchPapers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "ResearchPapers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisJobs_PaperId",
                table: "AnalysisJobs",
                column: "PaperId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisJobs_Status",
                table: "AnalysisJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CoverageReports_TopicId",
                table: "CoverageReports",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_DatasetPatterns_TopicId_DatasetName_Year",
                table: "DatasetPatterns",
                columns: new[] { "TopicId", "DatasetName", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_GapTimelines_TopicId_Year_GapType",
                table: "GapTimelines",
                columns: new[] { "TopicId", "Year", "GapType" });

            migrationBuilder.CreateIndex(
                name: "IX_LimitationPatterns_TopicId_LimitationText_Year",
                table: "LimitationPatterns",
                columns: new[] { "TopicId", "LimitationText", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_MethodPatterns_TopicId_MethodName_Year",
                table: "MethodPatterns",
                columns: new[] { "TopicId", "MethodName", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_PaperAnalyses_PaperId",
                table: "PaperAnalyses",
                column: "PaperId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperQualities_PaperId",
                table: "PaperQualities",
                column: "PaperId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResearchGapEvidences_PaperId",
                table: "ResearchGapEvidences",
                column: "PaperId");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchGapEvidences_ResearchGapId",
                table: "ResearchGapEvidences",
                column: "ResearchGapId");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchGaps_GapType",
                table: "ResearchGaps",
                column: "GapType");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchGaps_TopicId",
                table: "ResearchGaps",
                column: "TopicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisJobs");

            migrationBuilder.DropTable(
                name: "CoverageReports");

            migrationBuilder.DropTable(
                name: "DatasetPatterns");

            migrationBuilder.DropTable(
                name: "GapTimelines");

            migrationBuilder.DropTable(
                name: "LimitationPatterns");

            migrationBuilder.DropTable(
                name: "MethodPatterns");

            migrationBuilder.DropTable(
                name: "PaperAnalyses");

            migrationBuilder.DropTable(
                name: "PaperQualities");

            migrationBuilder.DropTable(
                name: "ResearchGapEvidences");

            migrationBuilder.DropTable(
                name: "ResearchGaps");
        }
    }
}
