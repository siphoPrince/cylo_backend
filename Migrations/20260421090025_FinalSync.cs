using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cylo_Backend.Migrations
{
    /// <inheritdoc />
    public partial class FinalSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // COMMENT THESE OUT because they already exist in the SQL table
            /*
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: true);
            */

            // KEEP THESE because we need them for the Explore Feed
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Posts",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Posts",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Province",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Posts");
        }
    }
}
