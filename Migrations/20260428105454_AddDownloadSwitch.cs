using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotographyWorkflow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadSwitch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanDownload",
                table: "Albums",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanDownload",
                table: "Albums");
        }
    }
}
