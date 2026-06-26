using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncProposalPendingPaperV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceName",
                table: "SyncProposals",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceName",
                table: "SyncProposals");
        }
    }
}
