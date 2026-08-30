using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueline.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowRepeatedTeamAbbreviations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_Abbrev",
                table: "Teams");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Abbrev",
                table: "Teams",
                column: "Abbrev");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_Abbrev",
                table: "Teams");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Abbrev",
                table: "Teams",
                column: "Abbrev",
                unique: true);
        }
    }
}
