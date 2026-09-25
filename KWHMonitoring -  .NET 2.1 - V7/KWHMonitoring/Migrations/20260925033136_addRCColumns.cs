using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class addRCColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RC",
                table: "RelayControl",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RC",
                table: "RelayControl");
        }
    }
}
