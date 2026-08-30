using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueline.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecordFailedGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailedGameIds",
                table: "IngestionRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GamesFailed",
                table: "IngestionRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedGameIds",
                table: "IngestionRuns");

            migrationBuilder.DropColumn(
                name: "GamesFailed",
                table: "IngestionRuns");
        }
    }
}
