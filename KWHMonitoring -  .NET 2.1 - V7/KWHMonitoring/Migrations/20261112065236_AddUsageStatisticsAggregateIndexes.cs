using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddUsageStatisticsAggregateIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_YearlyEnergy_Device_Year",
                table: "YearlyEnergy",
                columns: new[] { "DeviceKey", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyEnergy_Device_Year_Month",
                table: "MonthlyEnergy",
                columns: new[] { "DeviceKey", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_HourlyEnergy_Device_Hour",
                table: "HourlyEnergy",
                columns: new[] { "DeviceKey", "Hour" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyEnergy_Device_Date",
                table: "DailyEnergy",
                columns: new[] { "DeviceKey", "Date" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_YearlyEnergy_Device_Year",
                table: "YearlyEnergy");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyEnergy_Device_Year_Month",
                table: "MonthlyEnergy");

            migrationBuilder.DropIndex(
                name: "IX_HourlyEnergy_Device_Hour",
                table: "HourlyEnergy");

            migrationBuilder.DropIndex(
                name: "IX_DailyEnergy_Device_Date",
                table: "DailyEnergy");
        }
    }
}
