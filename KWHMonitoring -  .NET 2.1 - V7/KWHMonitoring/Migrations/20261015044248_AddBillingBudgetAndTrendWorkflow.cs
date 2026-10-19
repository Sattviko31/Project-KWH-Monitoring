using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddBillingBudgetAndTrendWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingAnomalyThresholdMode",
                table: "DeviceSettings",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BillingAnomalyThresholdPercent",
                table: "DeviceSettings",
                type: "decimal(9,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BillingTrendToleranceAmount",
                table: "DeviceSettings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingTrendToleranceMode",
                table: "DeviceSettings",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BillingTrendTolerancePercent",
                table: "DeviceSettings",
                type: "decimal(9,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyBillingBudgetAmount",
                table: "DeviceSettings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyBillingBudgetBaselineVersion",
                table: "DeviceSettings",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MonthlyBillingBudgetManualOverride",
                table: "DeviceSettings",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MonthlyBillingBudgetSource",
                table: "DeviceSettings",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MonthlyBillingBudgetTrendProfileId",
                table: "DeviceSettings",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MonthlyBillingBudgetUpdatedAt",
                table: "DeviceSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BillingBudgetHistories",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    PreviousAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Source = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    TrendProfileId = table.Column<long>(nullable: true),
                    BaselineVersion = table.Column<int>(nullable: true),
                    IdempotencyKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingBudgetHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillingConfirmationTokens",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Purpose = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    TrendProfileId = table.Column<long>(nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingConfirmationTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillingNotificationOutbox",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    EventKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    NotificationType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingNotificationOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillingTrendConfirmations",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    TrendProfileId = table.Column<long>(nullable: false),
                    AnomalyLogId = table.Column<long>(nullable: true),
                    DeviceKey = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    BillingPeriod = table.Column<DateTime>(type: "date", nullable: false),
                    ObservedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Decision = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Channel = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingTrendConfirmations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillingTrendProfiles",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    AnchorBillingPeriod = table.Column<DateTime>(type: "date", nullable: false),
                    AnchorAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ActiveBaselineAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BaselineVersion = table.Column<int>(nullable: false),
                    ValidConfirmationCount = table.Column<int>(nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingTrendProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingBudgetHistories_IdempotencyKey",
                table: "BillingBudgetHistories",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingConfirmationTokens_Hash_Purpose",
                table: "BillingConfirmationTokens",
                columns: new[] { "TokenHash", "Purpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingOutbox_Event_Channel_Type",
                table: "BillingNotificationOutbox",
                columns: new[] { "EventKey", "Channel", "NotificationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingTrendConfirmations_Device_Period",
                table: "BillingTrendConfirmations",
                columns: new[] { "DeviceKey", "BillingPeriod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingTrendProfiles_Device_Status",
                table: "BillingTrendProfiles",
                columns: new[] { "DeviceKey", "Status" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingBudgetHistories");

            migrationBuilder.DropTable(
                name: "BillingConfirmationTokens");

            migrationBuilder.DropTable(
                name: "BillingNotificationOutbox");

            migrationBuilder.DropTable(
                name: "BillingTrendConfirmations");

            migrationBuilder.DropTable(
                name: "BillingTrendProfiles");

            migrationBuilder.DropColumn(
                name: "BillingAnomalyThresholdMode",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "BillingAnomalyThresholdPercent",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "BillingTrendToleranceAmount",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "BillingTrendToleranceMode",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "BillingTrendTolerancePercent",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "MonthlyBillingBudgetAmount",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "MonthlyBillingBudgetBaselineVersion",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "MonthlyBillingBudgetManualOverride",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "MonthlyBillingBudgetSource",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "MonthlyBillingBudgetTrendProfileId",
                table: "DeviceSettings");

            migrationBuilder.DropColumn(
                name: "MonthlyBillingBudgetUpdatedAt",
                table: "DeviceSettings");
        }
    }
}
