using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AnomalyCenterPhase1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ═══════════════════════════════════════════════
            // 1. AnomalyLogs: tambah kolom baru
            // ═══════════════════════════════════════════════
            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedBy",
                table: "AnomalyLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedBy",
                table: "AnomalyLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedTime",
                table: "AnomalyLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsResolved",
                table: "AnomalyLogs",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OperatorAction",
                table: "AnomalyLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatorNotes",
                table: "AnomalyLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "AnomalyLogs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                defaultValue: "medium");

            migrationBuilder.AddColumn<string>(
                name: "RootCause",
                table: "AnomalyLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecommendedAction",
                table: "AnomalyLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyLogs_Severity",
                table: "AnomalyLogs",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyLogs_IsResolved",
                table: "AnomalyLogs",
                column: "IsResolved");

            // ═══════════════════════════════════════════════
            // 2. AnomalyChartSnapshots
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "AnomalyChartSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    AnomalyLogId = table.Column<long>(nullable: false),
                    DetectedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BeforeDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpperThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LowerThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EMAValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SnapshotStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, defaultValue: "before"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnomalyChartSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnomalyChartSnapshots_AnomalyLogs_AnomalyLogId",
                        column: x => x.AnomalyLogId,
                        principalTable: "AnomalyLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyChartSnapshots_AnomalyLogId",
                table: "AnomalyChartSnapshots",
                column: "AnomalyLogId",
                unique: true);

            // ═══════════════════════════════════════════════
            // 3. AnomalyMonthlyReports
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "AnomalyMonthlyReports",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Year = table.Column<int>(nullable: false),
                    Month = table.Column<int>(nullable: false),
                    TotalAnomalies = table.Column<int>(nullable: false),
                    OverloadCount = table.Column<int>(nullable: false),
                    DropCount = table.Column<int>(nullable: false),
                    AffectedDevices = table.Column<int>(nullable: false),
                    AverageDeviation = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TopAffectedDevice = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SummaryText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Recommendations = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GeneratedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnomalyMonthlyReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyMonthlyReports_Year_Month",
                table: "AnomalyMonthlyReports",
                columns: new[] { "Year", "Month" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnomalyChartSnapshots");

            migrationBuilder.DropTable(
                name: "AnomalyMonthlyReports");

            migrationBuilder.DropIndex(
                name: "IX_AnomalyLogs_Severity",
                table: "AnomalyLogs");

            migrationBuilder.DropIndex(
                name: "IX_AnomalyLogs_IsResolved",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "AcknowledgedBy",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "ResolvedBy",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "ResolvedTime",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "IsResolved",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "OperatorAction",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "OperatorNotes",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "RootCause",
                table: "AnomalyLogs");

            migrationBuilder.DropColumn(
                name: "RecommendedAction",
                table: "AnomalyLogs");
        }
    }
}
