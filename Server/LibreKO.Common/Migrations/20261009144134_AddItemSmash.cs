using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibreKO.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddItemSmash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemGradeBonuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ExtFile = table.Column<int>(type: "int", nullable: false),
                    ExtensionId = table.Column<int>(type: "int", nullable: false),
                    Bonus = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemGradeBonuses", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ItemSmashClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    GradeClass = table.Column<int>(type: "int", nullable: false),
                    Band = table.Column<int>(type: "int", nullable: false),
                    Rolls = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSmashClasses", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ItemSmashRewards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSmashRewards", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemGradeBonuses");

            migrationBuilder.DropTable(
                name: "ItemSmashClasses");

            migrationBuilder.DropTable(
                name: "ItemSmashRewards");
        }
    }
}
