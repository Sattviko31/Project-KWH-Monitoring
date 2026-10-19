using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddBillingAnomalyEventUniqueIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnomalyLogs_BillingDedup",
                table: "AnomalyLogs");

            migrationBuilder.AlterColumn<string>(
                name: "BillingAnomalyType",
                table: "AnomalyLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyLogs_BillingDedup",
                table: "AnomalyLogs",
                columns: new[] { "DeviceKey", "BillingPeriod", "ErpBillingId", "AnomalyType" },
                unique: true,
                filter: "[AnomalyType] = 'BILLING_VARIANCE' AND [BillingPeriod] IS NOT NULL AND [ErpBillingId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnomalyLogs_BillingDedup",
                table: "AnomalyLogs");

            migrationBuilder.AlterColumn<string>(
                name: "BillingAnomalyType",
                table: "AnomalyLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyLogs_BillingDedup",
                table: "AnomalyLogs",
                columns: new[] { "AnomalyType", "BillingAnomalyType", "DeviceKey", "BillingPeriod", "ErpBillingId" },
                unique: true,
                filter: "[AnomalyType] = 'BILLING_VARIANCE' AND [BillingPeriod] IS NOT NULL AND [ErpBillingId] IS NOT NULL");
        }
    }
}
