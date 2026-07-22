using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingPaperTopicsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TopicsJson",
                table: "PendingPapers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TopicsJson",
                table: "PendingPapers");
        }
    }
}
