using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotographyWorkflow.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAlbumColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientName",
                table: "Albums",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "Albums",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Albums",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Albums",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientName",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Albums");
        }
    }
}
