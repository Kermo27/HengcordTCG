using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HengcordTCG.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddGameStatsAndMatchResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Cards");

            migrationBuilder.AddColumn<string>(
                name: "AbilityId",
                table: "Cards",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AbilityText",
                table: "Cards",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CardType",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CounterStrike",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DieSize",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Health",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "Cards",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LightCost",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Speed",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MatchResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WinnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    LoserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Turns = table.Column<int>(type: "INTEGER", nullable: false),
                    WinnerHpRemaining = table.Column<int>(type: "INTEGER", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchResults_Users_LoserId",
                        column: x => x.LoserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchResults_Users_WinnerId",
                        column: x => x.WinnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_LoserId",
                table: "MatchResults",
                column: "LoserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_WinnerId",
                table: "MatchResults",
                column: "WinnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchResults");

            migrationBuilder.DropColumn(
                name: "AbilityId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "AbilityText",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CardType",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CounterStrike",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "DieSize",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Health",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "LightCost",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Speed",
                table: "Cards");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Cards",
                type: "TEXT",
                nullable: true);
        }
    }
}
