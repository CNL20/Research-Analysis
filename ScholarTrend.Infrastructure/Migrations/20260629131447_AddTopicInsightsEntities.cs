using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicInsightsEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaperTopicExtractions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaperId = table.Column<int>(type: "int", nullable: false),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    MethodsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DatasetsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LimitationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FutureWorkJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AchievementHint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExtractedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperTopicExtractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaperTopicExtractions_ResearchPapers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "ResearchPapers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaperTopicExtractions_ResearchTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "ResearchTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TopicInsightJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PapersProcessed = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicInsightJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicInsightJobs_ResearchTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "ResearchTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TopicInsights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Achievement = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResearchGapsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FutureDirectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TopMethodsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TopDatasetsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaperCountAtGeneration = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicInsights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicInsights_ResearchTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "ResearchTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TopicInsightEvidences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopicInsightId = table.Column<int>(type: "int", nullable: false),
                    PaperId = table.Column<int>(type: "int", nullable: false),
                    EvidenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Excerpt = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicInsightEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicInsightEvidences_ResearchPapers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "ResearchPapers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TopicInsightEvidences_TopicInsights_TopicInsightId",
                        column: x => x.TopicInsightId,
                        principalTable: "TopicInsights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaperTopicExtractions_PaperId",
                table: "PaperTopicExtractions",
                column: "PaperId");

            migrationBuilder.CreateIndex(
                name: "IX_PaperTopicExtractions_TopicId",
                table: "PaperTopicExtractions",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicInsightEvidences_PaperId",
                table: "TopicInsightEvidences",
                column: "PaperId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicInsightEvidences_TopicInsightId",
                table: "TopicInsightEvidences",
                column: "TopicInsightId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicInsightJobs_TopicId",
                table: "TopicInsightJobs",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicInsights_TopicId",
                table: "TopicInsights",
                column: "TopicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaperTopicExtractions");

            migrationBuilder.DropTable(
                name: "TopicInsightEvidences");

            migrationBuilder.DropTable(
                name: "TopicInsightJobs");

            migrationBuilder.DropTable(
                name: "TopicInsights");
        }
    }
}
