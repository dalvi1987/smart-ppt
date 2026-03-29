using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPPT.Infrastructure.Migrations
{
    public partial class AddScaffoldPathToTemplate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScaffoldPath",
                table: "Templates",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScaffoldPath",
                table: "Templates");
        }
    }
}
