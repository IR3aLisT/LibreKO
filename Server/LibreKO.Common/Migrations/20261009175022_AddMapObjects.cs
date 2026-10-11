using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibreKO.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddMapObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MapObjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ZoneId = table.Column<short>(type: "smallint", nullable: false),
                    Index = table.Column<short>(type: "smallint", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    ControlNpcId = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    X = table.Column<float>(type: "float", nullable: false),
                    Y = table.Column<float>(type: "float", nullable: false),
                    Z = table.Column<float>(type: "float", nullable: false),
                    Belong = table.Column<int>(type: "int", nullable: false),
                    Life = table.Column<byte>(type: "tinyint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapObjects", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MapObjects");
        }
    }
}
