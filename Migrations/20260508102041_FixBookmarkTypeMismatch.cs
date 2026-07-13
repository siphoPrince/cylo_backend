using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cylo_Backend.Migrations
{
    /// <inheritdoc />
    public partial class FixBookmarkTypeMismatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookmarks_Users_UserId1",
                table: "Bookmarks");

            migrationBuilder.DropIndex(
                name: "IX_Bookmarks_UserId1",
                table: "Bookmarks");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "Bookmarks");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Bookmarks",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookmarks_Users_UserId",
                table: "Bookmarks",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookmarks_Users_UserId",
                table: "Bookmarks");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Bookmarks",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "Bookmarks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_UserId1",
                table: "Bookmarks",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookmarks_Users_UserId1",
                table: "Bookmarks",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
