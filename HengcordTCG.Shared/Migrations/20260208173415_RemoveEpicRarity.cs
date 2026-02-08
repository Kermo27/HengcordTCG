using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HengcordTCG.Shared.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEpicRarity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Convert Epic (2) to Rare (1)
            migrationBuilder.Sql("UPDATE Cards SET Rarity = 1 WHERE Rarity = 2;");
            
            // 2. Convert Legendary (3) to Legendary (2)
            migrationBuilder.Sql("UPDATE Cards SET Rarity = 2 WHERE Rarity = 3;");

            migrationBuilder.DropColumn(
                name: "ChanceEpic",
                table: "PackTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChanceEpic",
                table: "PackTypes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
