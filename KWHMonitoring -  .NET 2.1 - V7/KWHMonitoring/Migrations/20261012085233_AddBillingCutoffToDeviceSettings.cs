using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddBillingCutoffToDeviceSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingCutoffDay",
                table: "DeviceSettings",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BillingCutoffEnabled",
                table: "DeviceSettings",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingCutoffDay",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "BillingCutoffEnabled",
                table: "DeviceSettings");
        }
    }
}
