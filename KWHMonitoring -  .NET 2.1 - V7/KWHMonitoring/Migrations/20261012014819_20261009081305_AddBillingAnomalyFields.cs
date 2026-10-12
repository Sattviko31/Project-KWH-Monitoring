using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class _20261009081305_AddBillingAnomalyFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.AddColumn<decimal>(
            //     name: "BillingAnomalyThreshold",
            //     table: "DeviceSettings",
            //     type: "decimal(18,2)",
            //     nullable: true);

            // migrationBuilder.AddColumn<decimal>(
            //     name: "ActualBilling",
            //     table: "AnomalyLogs",
            //     type: "decimal(18,2)",
            //     nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingAnomalyType",
                table: "AnomalyLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            // migrationBuilder.AddColumn<decimal>(
            //     name: "BillingDifference",
            //     table: "AnomalyLogs",
            //     type: "decimal(18,2)",
            //     nullable: true);

            // migrationBuilder.AddColumn<decimal>(
            //     name: "BillingDifferencePercent",
            //     table: "AnomalyLogs",
            //     type: "decimal(18,4)",
            //     nullable: true);

            // migrationBuilder.AddColumn<DateTime>(
            //     name: "BillingPeriod",
            //     table: "AnomalyLogs",
            //     type: "date",
            //     nullable: true);

            // migrationBuilder.AddColumn<long>(
            //     name: "ErpBillingId",
            //     table: "AnomalyLogs",
            //     nullable: true);

            // migrationBuilder.AddColumn<decimal>(
            //     name: "EstimatedBilling",
            //     table: "AnomalyLogs",
            //     type: "decimal(18,2)",
            //     nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousBilling",
                table: "AnomalyLogs",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PreviousBillingPeriod",
                table: "AnomalyLogs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PreviousErpBillingId",
                table: "AnomalyLogs",
                nullable: true);

            // migrationBuilder.CreateIndex(
            //     name: "IX_AnomalyLogs_BillingDedup",
            //     table: "AnomalyLogs",
            //     columns: new[] { "AnomalyType", "BillingAnomalyType", "DeviceKey", "BillingPeriod", "ErpBillingId" },
            //     unique: true,
            //     filter: "[AnomalyType] = 'BILLING_VARIANCE' AND [BillingPeriod] IS NOT NULL AND [ErpBillingId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnomalyLogs_BillingDedup",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "BillingAnomalyThreshold",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "ActualBilling",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "BillingAnomalyType",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "BillingDifference",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "BillingDifferencePercent",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "BillingPeriod",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "ErpBillingId",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "EstimatedBilling",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "PreviousBilling",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "PreviousBillingPeriod",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "PreviousErpBillingId",
                table: "AnomalyLogs");
        }
    }
}
