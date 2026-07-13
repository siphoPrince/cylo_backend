using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cylo_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPostToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PostId",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PostId",
                table: "Orders",
                column: "PostId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Posts_PostId",
                table: "Orders",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Posts_PostId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PostId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PostId",
                table: "Orders");
        }
    }
}
