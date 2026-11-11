using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddEnergyProjectionTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnergyProjectionGaps",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    GapStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GapEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    DetectionSource = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnergyProjectionGaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnergyProjectionValues",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    BucketStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProjectedKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LowerBoundKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    UpperBoundKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(9,4)", nullable: true),
                    ConfidenceLevel = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ProjectionMethod = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    SampleCount = table.Column<int>(nullable: false),
                    GapId = table.Column<long>(nullable: true),
                    SourcePeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourcePeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CalculationVersion = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReconciledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnergyProjectionValues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnergyProjectionGaps_Status",
                table: "EnergyProjectionGaps",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EnergyProjectionGaps_Open_Device_Start",
                table: "EnergyProjectionGaps",
                columns: new[] { "Status", "DeviceKey", "GapStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EnergyProjectionGaps_Device_GapStart",
                table: "EnergyProjectionGaps",
                columns: new[] { "DeviceKey", "GapStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EnergyProjectionValues_Gap_Status",
                table: "EnergyProjectionValues",
                columns: new[] { "GapId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EnergyProjectionValues_Status_Bucket",
                table: "EnergyProjectionValues",
                columns: new[] { "Status", "BucketStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EnergyProjectionValues_Device_Bucket_Version",
                table: "EnergyProjectionValues",
                columns: new[] { "DeviceKey", "BucketStartUtc", "CalculationVersion" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnergyProjectionGaps");

            migrationBuilder.DropTable(
                name: "EnergyProjectionValues");

        }
    }
}
