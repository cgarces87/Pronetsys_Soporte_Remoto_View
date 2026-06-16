using Microsoft.EntityFrameworkCore.Migrations;

namespace Pronetsys.Server.Migrations.Sqlite;

public partial class Temppassword : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TempPassword",
            table: "PronetsysUsers",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TempPassword",
            table: "PronetsysUsers");
    }
}
