using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotographyWorkflow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCanSelectSwitch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanSelect",
                table: "Albums",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanSelect",
                table: "Albums");
        }
    }
}
