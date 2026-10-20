using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddBillingEstimateSnapshots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillingEstimateSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    ReportType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    PeriodStart = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    DeviceKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    EnergyKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WbpKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LwbpKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    FlatTariffPerKWh = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TariffWBP = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TariffLWBP = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WbpCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LwbpCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TariffMode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    DataStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    CalculatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<System.DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingEstimateSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingEstimateSnapshots_Report_Period_Device",
                table: "BillingEstimateSnapshots",
                columns: new[] { "ReportType", "PeriodStart", "PeriodEnd", "DeviceKey" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BillingEstimateSnapshots");
        }
    }
}
