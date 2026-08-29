using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blueline.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngestionRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: true),
                    StartedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GamesIngested = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    HeadshotUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Abbrev = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LogoUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<int>(type: "INTEGER", nullable: false),
                    GameType = table.Column<int>(type: "INTEGER", nullable: false),
                    GameDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    HomeTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    HomeScore = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayScore = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPeriodType = table.Column<string>(type: "TEXT", nullable: false),
                    GameState = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Games_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Games_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalieGameStats",
                columns: table => new
                {
                    GameId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    Starter = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShotsAgainst = table.Column<int>(type: "INTEGER", nullable: false),
                    Saves = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "INTEGER", nullable: false),
                    Pim = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeOnIceSeconds = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalieGameStats", x => new { x.GameId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_GoalieGameStats_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoalieGameStats_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoalieGameStats_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SkaterGameStats",
                columns: table => new
                {
                    GameId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    Goals = table.Column<int>(type: "INTEGER", nullable: false),
                    Assists = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    PlusMinus = table.Column<int>(type: "INTEGER", nullable: false),
                    Pim = table.Column<int>(type: "INTEGER", nullable: false),
                    Hits = table.Column<int>(type: "INTEGER", nullable: false),
                    BlockedShots = table.Column<int>(type: "INTEGER", nullable: false),
                    Shots = table.Column<int>(type: "INTEGER", nullable: false),
                    PowerPlayGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    Giveaways = table.Column<int>(type: "INTEGER", nullable: false),
                    Takeaways = table.Column<int>(type: "INTEGER", nullable: false),
                    Shifts = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeOnIceSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    FaceoffWinPctg = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkaterGameStats", x => new { x.GameId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_SkaterGameStats_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkaterGameStats_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkaterGameStats_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamGameStats",
                columns: table => new
                {
                    GameId = table.Column<long>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    OpponentTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsHome = table.Column<bool>(type: "INTEGER", nullable: false),
                    GoalsFor = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "INTEGER", nullable: false),
                    Result = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamGameStats", x => new { x.GameId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_TeamGameStats_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamGameStats_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_AwayTeamId",
                table: "Games",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_HomeTeamId",
                table: "Games",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_SeasonId_GameType_GameDate",
                table: "Games",
                columns: new[] { "SeasonId", "GameType", "GameDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalieGameStats_PlayerId",
                table: "GoalieGameStats",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalieGameStats_TeamId",
                table: "GoalieGameStats",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_LastName",
                table: "Players",
                column: "LastName");

            migrationBuilder.CreateIndex(
                name: "IX_SkaterGameStats_PlayerId",
                table: "SkaterGameStats",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_SkaterGameStats_TeamId",
                table: "SkaterGameStats",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamGameStats_TeamId",
                table: "TeamGameStats",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Abbrev",
                table: "Teams",
                column: "Abbrev",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoalieGameStats");

            migrationBuilder.DropTable(
                name: "IngestionRuns");

            migrationBuilder.DropTable(
                name: "SkaterGameStats");

            migrationBuilder.DropTable(
                name: "TeamGameStats");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Teams");
        }
    }
}
