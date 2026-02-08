using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HengcordTCG.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddPackSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExclusivePackId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PackTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<int>(type: "INTEGER", nullable: false),
                    ChanceCommon = table.Column<int>(type: "INTEGER", nullable: false),
                    ChanceRare = table.Column<int>(type: "INTEGER", nullable: false),
                    ChanceEpic = table.Column<int>(type: "INTEGER", nullable: false),
                    ChanceLegendary = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_ExclusivePackId",
                table: "Cards",
                column: "ExclusivePackId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_PackTypes_ExclusivePackId",
                table: "Cards",
                column: "ExclusivePackId",
                principalTable: "PackTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_PackTypes_ExclusivePackId",
                table: "Cards");

            migrationBuilder.DropTable(
                name: "PackTypes");

            migrationBuilder.DropIndex(
                name: "IX_Cards_ExclusivePackId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ExclusivePackId",
                table: "Cards");
        }
    }
}
