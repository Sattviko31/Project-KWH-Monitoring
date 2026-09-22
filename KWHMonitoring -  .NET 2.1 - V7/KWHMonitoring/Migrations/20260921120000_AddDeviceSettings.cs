using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddDeviceSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    MaxCapacity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DeviceCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DowntimeEnabled = table.Column<bool>(nullable: false),
                    DowntimeStart = table.Column<TimeSpan>(type: "time", nullable: false),
                    DowntimeEnd = table.Column<TimeSpan>(type: "time", nullable: false),
                    TariffPerKWh = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LoadNormalThreshold = table.Column<int>(nullable: false),
                    LoadMediumThreshold = table.Column<int>(nullable: false),
                    EmaUpperThreshold = table.Column<int>(nullable: false),
                    EmaLowerThreshold = table.Column<int>(nullable: false),
                    EmaFibUpper = table.Column<double>(type: "float", nullable: false),
                    EmaFibLower = table.Column<double>(type: "float", nullable: false),
                    ControlMode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSettings_DeviceKey",
                table: "DeviceSettings",
                column: "DeviceKey",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceSettings");
        }
    }
}
