using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cylo_Backend.Migrations
{
    /// <inheritdoc />
    public partial class FinalDatabaseSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EscrowOrders_Users_BuyerId",
                table: "EscrowOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_EscrowOrders_Users_SellerId",
                table: "EscrowOrders");

            migrationBuilder.AddForeignKey(
                name: "FK_EscrowOrders_Users_BuyerId",
                table: "EscrowOrders",
                column: "BuyerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EscrowOrders_Users_SellerId",
                table: "EscrowOrders",
                column: "SellerId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EscrowOrders_Users_BuyerId",
                table: "EscrowOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_EscrowOrders_Users_SellerId",
                table: "EscrowOrders");

            migrationBuilder.AddForeignKey(
                name: "FK_EscrowOrders_Users_BuyerId",
                table: "EscrowOrders",
                column: "BuyerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscrowOrders_Users_SellerId",
                table: "EscrowOrders",
                column: "SellerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
