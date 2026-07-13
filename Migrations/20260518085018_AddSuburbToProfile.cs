using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cylo_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSuburbToProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Suburb",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Suburb",
                table: "Profiles");
        }
    }
}
