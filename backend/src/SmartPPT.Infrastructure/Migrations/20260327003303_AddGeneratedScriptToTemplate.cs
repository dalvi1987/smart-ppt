using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPPT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneratedScriptToTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GeneratedScript",
                table: "Templates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScriptGeneratedAt",
                table: "Templates",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeneratedScript",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "ScriptGeneratedAt",
                table: "Templates");
        }
    }
}
