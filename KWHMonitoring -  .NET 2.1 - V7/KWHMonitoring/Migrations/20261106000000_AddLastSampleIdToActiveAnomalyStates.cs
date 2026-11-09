using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddLastSampleIdToActiveAnomalyStates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LastSampleId",
                table: "ActiveAnomalyStates",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSampleId",
                table: "ActiveAnomalyStates");
        }
    }
}