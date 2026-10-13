using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibreKO.Common.Migrations
{
    public partial class AddJuraidWarnings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "JuraidWarnedAt",
                table: "Characters",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "JuraidWarnings",
                table: "Characters",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JuraidWarnedAt",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "JuraidWarnings",
                table: "Characters");
        }
    }
}
