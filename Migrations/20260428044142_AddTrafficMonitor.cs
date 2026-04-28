using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotographyWorkflow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTrafficMonitor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DownloadCount",
                table: "Albums",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TotalBytesDownloaded",
                table: "Albums",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DownloadCount",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "TotalBytesDownloaded",
                table: "Albums");
        }
    }
}
