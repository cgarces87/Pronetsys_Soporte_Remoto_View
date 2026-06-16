using Microsoft.EntityFrameworkCore.Migrations;

namespace Pronetsys.Server.Migrations.SqlServer;

public partial class IsSponsorproperty : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsSponsor",
            table: "Organizations",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsSponsor",
            table: "Organizations");
    }
}
