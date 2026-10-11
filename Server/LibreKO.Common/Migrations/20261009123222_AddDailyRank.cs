using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibreKO.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyRank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyRankCounters",
                columns: table => new
                {
                    CharacterId = table.Column<int>(type: "int", nullable: false),
                    MerchantGold = table.Column<long>(type: "bigint", nullable: false),
                    BlessedUpgrades = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRankCounters", x => x.CharacterId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DailyRankPlaces",
                columns: table => new
                {
                    Board = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    Place = table.Column<int>(type: "int", nullable: false),
                    PreviousPlace = table.Column<int>(type: "int", nullable: false),
                    RankedOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRankPlaces", x => new { x.Board, x.SubjectId });
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyRankCounters");

            migrationBuilder.DropTable(
                name: "DailyRankPlaces");
        }
    }
}
