using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cylo_Backend.Migrations
{
    /// <inheritdoc />
    public partial class addinghandlerow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HandleName",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HandleName",
                table: "Profiles");
        }
    }
}
