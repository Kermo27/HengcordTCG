using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HengcordTCG.Shared.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureTradeRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trades_Users_InitiatorId",
                table: "Trades");

            migrationBuilder.DropForeignKey(
                name: "FK_Trades_Users_TargetId",
                table: "Trades");

            migrationBuilder.AddForeignKey(
                name: "FK_Trades_Users_InitiatorId",
                table: "Trades",
                column: "InitiatorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Trades_Users_TargetId",
                table: "Trades",
                column: "TargetId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trades_Users_InitiatorId",
                table: "Trades");

            migrationBuilder.DropForeignKey(
                name: "FK_Trades_Users_TargetId",
                table: "Trades");

            migrationBuilder.AddForeignKey(
                name: "FK_Trades_Users_InitiatorId",
                table: "Trades",
                column: "InitiatorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Trades_Users_TargetId",
                table: "Trades",
                column: "TargetId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
