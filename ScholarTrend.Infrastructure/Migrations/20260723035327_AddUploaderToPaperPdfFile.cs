using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarTrend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUploaderToPaperPdfFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UploadedById",
                table: "PaperPdfFiles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperPdfFiles_UploadedById",
                table: "PaperPdfFiles",
                column: "UploadedById");

            migrationBuilder.AddForeignKey(
                name: "FK_PaperPdfFiles_AspNetUsers_UploadedById",
                table: "PaperPdfFiles",
                column: "UploadedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaperPdfFiles_AspNetUsers_UploadedById",
                table: "PaperPdfFiles");

            migrationBuilder.DropIndex(
                name: "IX_PaperPdfFiles_UploadedById",
                table: "PaperPdfFiles");

            migrationBuilder.DropColumn(
                name: "UploadedById",
                table: "PaperPdfFiles");
        }
    }
}
