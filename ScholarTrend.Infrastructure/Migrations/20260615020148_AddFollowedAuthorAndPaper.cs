using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowedAuthorAndPaper : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FollowedAuthors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AuthorId = table.Column<int>(type: "int", nullable: false),
                    FollowedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowedAuthors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FollowedAuthors_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FollowedAuthors_Authors_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Authors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FollowedPapers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PaperId = table.Column<int>(type: "int", nullable: false),
                    FollowedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowedPapers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FollowedPapers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FollowedPapers_ResearchPapers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "ResearchPapers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FollowedAuthors_AuthorId",
                table: "FollowedAuthors",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowedAuthors_UserId",
                table: "FollowedAuthors",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowedAuthors_UserId_AuthorId",
                table: "FollowedAuthors",
                columns: new[] { "UserId", "AuthorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FollowedPapers_PaperId",
                table: "FollowedPapers",
                column: "PaperId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowedPapers_UserId",
                table: "FollowedPapers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FollowedPapers_UserId_PaperId",
                table: "FollowedPapers",
                columns: new[] { "UserId", "PaperId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FollowedAuthors");

            migrationBuilder.DropTable(
                name: "FollowedPapers");
        }
    }
}
