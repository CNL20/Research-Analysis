using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ViewCount",
                table: "ResearchPapers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ViewCount",
                table: "ResearchPapers",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
