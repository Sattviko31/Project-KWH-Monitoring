using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace KWHMonitoring.Migrations
{
    public partial class MigrationAddKWHDataHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KWHData_History",
                columns: table => new
                {
                    HistoryId = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    OriginalId = table.Column<long>(nullable: false),
                    DeviceKey = table.Column<string>(maxLength: 100, nullable: false),
                    TerminalTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupName = table.Column<string>(maxLength: 100, nullable: true),
                    DeviceId = table.Column<string>(maxLength: 100, nullable: true),
                    PHASE_R = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PHASE_S = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PHASE_T = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AMPERE_R = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AMPERE_S = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AMPERE_T = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    W = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CosPhi = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    F = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Aktif_Power = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalW = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalW1M = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KWHData_History", x => x.HistoryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KWHData_History_DeviceKey",
                table: "KWHData_History",
                column: "DeviceKey");

            migrationBuilder.CreateIndex(
                name: "IX_KWHData_History_ReceivedTime",
                table: "KWHData_History",
                column: "ReceivedTime");

            migrationBuilder.CreateIndex(
                name: "IX_KWHData_History_DeviceKey_ReceivedTime",
                table: "KWHData_History",
                columns: new[] { "DeviceKey", "ReceivedTime" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "KWHData_History");
        }
    }
}
