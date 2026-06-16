using Microsoft.EntityFrameworkCore.Migrations;

namespace Pronetsys.Server.Migrations.Sqlite;

public partial class IsServerAdminproperty : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsServerAdmin",
            table: "PronetsysUsers",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsServerAdmin",
            table: "PronetsysUsers");
    }
}
