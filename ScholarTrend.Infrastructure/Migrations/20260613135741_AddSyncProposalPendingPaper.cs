using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncProposalPendingPaper : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalFetched = table.Column<int>(type: "int", nullable: false),
                    TotalApproved = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncProposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingPapers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SyncProposalId = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExternalSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Abstract = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    Year = table.Column<int>(type: "int", nullable: true),
                    CitationCount = table.Column<int>(type: "int", nullable: true),
                    Doi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AuthorNamesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ImportedPaperId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingPapers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingPapers_SyncProposals_SyncProposalId",
                        column: x => x.SyncProposalId,
                        principalTable: "SyncProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingPapers_ExternalId_ExternalSource",
                table: "PendingPapers",
                columns: new[] { "ExternalId", "ExternalSource" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingPapers_Status",
                table: "PendingPapers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PendingPapers_SyncProposalId",
                table: "PendingPapers",
                column: "SyncProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncProposals_CreatedAt",
                table: "SyncProposals",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SyncProposals_Status",
                table: "SyncProposals",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingPapers");

            migrationBuilder.DropTable(
                name: "SyncProposals");
        }
    }
}
