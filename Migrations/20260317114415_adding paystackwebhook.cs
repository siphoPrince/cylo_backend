using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cylo_Backend.Migrations
{
    /// <inheritdoc />
    public partial class addingpaystackwebhook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaystackSubaccountCode",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BuyerConfirmed",
                table: "EscrowOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SellerConfirmed",
                table: "EscrowOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_EscrowOrders_PostId",
                table: "EscrowOrders",
                column: "PostId");

            migrationBuilder.AddForeignKey(
                name: "FK_EscrowOrders_Posts_PostId",
                table: "EscrowOrders",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EscrowOrders_Posts_PostId",
                table: "EscrowOrders");

            migrationBuilder.DropIndex(
                name: "IX_EscrowOrders_PostId",
                table: "EscrowOrders");

            migrationBuilder.DropColumn(
                name: "PaystackSubaccountCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BuyerConfirmed",
                table: "EscrowOrders");

            migrationBuilder.DropColumn(
                name: "SellerConfirmed",
                table: "EscrowOrders");
        }
    }
}
