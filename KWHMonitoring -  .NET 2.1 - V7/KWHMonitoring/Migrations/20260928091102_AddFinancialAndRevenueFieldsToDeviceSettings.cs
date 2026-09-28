using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddFinancialAndRevenueFieldsToDeviceSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BudgetKWh",
                table: "DeviceSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RevenuePerHour",
                table: "DeviceSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SurfaceArea",
                table: "DeviceSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TariffLWBP",
                table: "DeviceSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TariffWBP",
                table: "DeviceSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "WbpEndHour",
                table: "DeviceSettings",
                nullable: false,
                defaultValue: 22);

            migrationBuilder.AddColumn<int>(
                name: "WbpStartHour",
                table: "DeviceSettings",
                nullable: false,
                defaultValue: 18);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetKWh",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "RevenuePerHour",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "SurfaceArea",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "TariffLWBP",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "TariffWBP",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "WbpEndHour",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "WbpStartHour",
                table: "DeviceSettings");
        }
    }
}
