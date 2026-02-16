using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HengcordTCG.Shared.Migrations
{
    /// <inheritdoc />
    public partial class MinMaxDamage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DieSize",
                table: "Cards",
                newName: "MinDamage");

            migrationBuilder.AddColumn<int>(
                name: "MaxDamage",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxDamage",
                table: "Cards");

            migrationBuilder.RenameColumn(
                name: "MinDamage",
                table: "Cards",
                newName: "DieSize");
        }
    }
}
