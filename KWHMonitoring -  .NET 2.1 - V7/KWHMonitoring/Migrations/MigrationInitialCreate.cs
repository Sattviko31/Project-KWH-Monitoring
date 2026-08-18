using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace KWHMonitoring.Migrations
{
    public partial class MigrationInitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create KWHData table (main raw data storage)
            migrationBuilder.CreateTable(
                name: "KWHData",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(maxLength: 50, nullable: false),
                    DeviceId = table.Column<string>(maxLength: 50, nullable: false),
                    GroupName = table.Column<string>(maxLength: 100, nullable: false),
                    TerminalTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PHASE_R = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PHASE_S = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PHASE_T = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AMPERE_R = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AMPERE_S = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AMPERE_T = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CosPhi = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    W = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalW1M = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Aktif_Power = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalW = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    F = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KWHData", x => x.Id);
                });

            // Create AnomalyLogs table
            migrationBuilder.CreateTable(
                name: "AnomalyLogs",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(maxLength: 100, nullable: false),
                    DeviceId = table.Column<string>(maxLength: 100, nullable: false),
                    AnomalyType = table.Column<string>(maxLength: 500, nullable: false),
                    PowerValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ThresholdValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Deviation = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DetectedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EMAValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ThresholdMode = table.Column<string>(maxLength: 50, nullable: false),
                    Acknowledged = table.Column<bool>(nullable: false),
                    AcknowledgedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnomalyLogs", x => x.Id);
                });

            // Create AppSettings table
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    SettingKey = table.Column<string>(maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            // Create HourlyEnergy table
            migrationBuilder.CreateTable(
                name: "HourlyEnergy",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(maxLength: 100, nullable: false),
                    Hour = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnergyKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HourlyEnergy", x => x.Id);
                });

            // Create DailyEnergy table
            migrationBuilder.CreateTable(
                name: "DailyEnergy",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(maxLength: 100, nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    EnergyKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyEnergy", x => x.Id);
                });

            // Create MonthlyEnergy table
            migrationBuilder.CreateTable(
                name: "MonthlyEnergy",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(maxLength: 100, nullable: false),
                    Year = table.Column<int>(nullable: false),
                    Month = table.Column<int>(nullable: false),
                    EnergyKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyEnergy", x => x.Id);
                });

            // Create YearlyEnergy table
            migrationBuilder.CreateTable(
                name: "YearlyEnergy",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(maxLength: 100, nullable: false),
                    Year = table.Column<int>(nullable: false),
                    EnergyKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyEnergy", x => x.Id);
                });

            // Create indexes for performance optimization
            migrationBuilder.CreateIndex(
                name: "IX_KWHData_DeviceKey_TerminalTime",
                table: "KWHData",
                columns: new[] { "DeviceKey", "TerminalTime" });

            migrationBuilder.CreateIndex(
                name: "IX_KWHData_ReceivedTime",
                table: "KWHData",
                column: "ReceivedTime");

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyLogs_DeviceKey_DetectedTime",
                table: "AnomalyLogs",
                columns: new[] { "DeviceKey", "DetectedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyLogs_Acknowledged",
                table: "AnomalyLogs",
                column: "Acknowledged");

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_SettingKey",
                table: "AppSettings",
                column: "SettingKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HourlyEnergy_DeviceKey_Hour",
                table: "HourlyEnergy",
                columns: new[] { "DeviceKey", "Hour" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyEnergy_DeviceKey_Date",
                table: "DailyEnergy",
                columns: new[] { "DeviceKey", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyEnergy_DeviceKey_Year_Month",
                table: "MonthlyEnergy",
                columns: new[] { "DeviceKey", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyEnergy_DeviceKey_Year",
                table: "YearlyEnergy",
                columns: new[] { "DeviceKey", "Year" },
                unique: true);

            // Seed default app settings
            migrationBuilder.Sql(@"
                INSERT INTO [AppSettings] ([SettingKey], [SettingValue], [UpdatedAt]) VALUES
                ('MaxCapacity', '100000', GETDATE()),
                ('NotificationEnabled', 'false', GETDATE()),
                ('CheckIntervalSeconds', '30', GETDATE());
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // // ⚠️ WARNING: JANGAN JALANKAN DI PRODUCTION!
            // // Metode ini adalah ROLLBACK migration dan akan MENGHAPUS SEMUA DATA!
            // // Hanya gunakan untuk development/testing environment saat mau reset database.
            // // 
            // // Jika Anda tidak sengaja menjalankan ini, semua data di tabel berikut akan hilang:
            // // - KWHData (data monitoring device)
            // // - AnomalyLogs (log anomali)
            // // - AppSettings (konfigurasi)
            // // - HourlyEnergy, DailyEnergy, MonthlyEnergy, YearlyEnergy (data agregat)
            // //
            // // Recommended action di production: Biarkan kosong atau comment out semua kode di bawah.

            // #if DEBUG
            // // Development mode: Drop tables for testing purposes only
            // migrationBuilder.Sql("DELETE FROM [AppSettings]");
            
            // migrationBuilder.DropTable(name: "YearlyEnergy");
            // migrationBuilder.DropTable(name: "MonthlyEnergy");
            // migrationBuilder.DropTable(name: "DailyEnergy");
            // migrationBuilder.DropTable(name: "HourlyEnergy");
            // migrationBuilder.DropTable(name: "AppSettings");
            // migrationBuilder.DropTable(name: "AnomalyLogs");
            // migrationBuilder.DropTable(name: "KWHData");
            // #else
            // // Production mode: DO NOTHING to prevent accidental data loss
            // // Uncomment below if you intentionally want to rollback:
            // // migrationBuilder.Sql("DELETE FROM [AppSettings]");
            // // migrationBuilder.DropTable(name: "YearlyEnergy");
            // // ... dll
            // #endif
        }
    }
}
