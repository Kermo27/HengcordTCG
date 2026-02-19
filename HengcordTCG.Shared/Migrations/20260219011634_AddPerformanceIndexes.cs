using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HengcordTCG.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserCards_UserId",
                table: "UserCards");

            migrationBuilder.CreateIndex(
                name: "IX_UserCards_UserId_CardId",
                table: "UserCards",
                columns: new[] { "UserId", "CardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_FinishedAt",
                table: "MatchResults",
                column: "FinishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Name",
                table: "Cards",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserCards_UserId_CardId",
                table: "UserCards");

            migrationBuilder.DropIndex(
                name: "IX_MatchResults_FinishedAt",
                table: "MatchResults");

            migrationBuilder.DropIndex(
                name: "IX_Cards_Name",
                table: "Cards");

            migrationBuilder.CreateIndex(
                name: "IX_UserCards_UserId",
                table: "UserCards",
                column: "UserId");
        }
    }
}
