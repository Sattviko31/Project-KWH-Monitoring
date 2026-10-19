using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddBillingPeriodDifferenceFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PeriodDifference",
                table: "AnomalyLogs",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PeriodDifferencePercent",
                table: "AnomalyLogs",
                type: "decimal(18,4)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PeriodDifference",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "PeriodDifferencePercent",
                table: "AnomalyLogs");
        }
    }
}