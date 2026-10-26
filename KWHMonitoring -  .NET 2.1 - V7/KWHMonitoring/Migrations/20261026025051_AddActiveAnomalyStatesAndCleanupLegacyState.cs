using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddActiveAnomalyStatesAndCleanupLegacyState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveAnomalyStates",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", Microsoft.EntityFrameworkCore.Metadata.SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AnomalyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConfirmationCount = table.Column<int>(nullable: false),
                    MaxConfirmations = table.Column<int>(nullable: false),
                    IsConfirmed = table.Column<bool>(nullable: false),
                    AnomalyLogId = table.Column<long>(nullable: true),
                    FirstDetectedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    LastDetectedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveAnomalyStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveAnomalyStates_DeviceKey",
                table: "ActiveAnomalyStates",
                column: "DeviceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActiveAnomalyStates_UpdatedAt",
                table: "ActiveAnomalyStates",
                column: "UpdatedAt");

            migrationBuilder.Sql("DELETE FROM [AppSettings] WHERE [SettingKey] LIKE 'AnomalyState.%';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ActiveAnomalyStates");
        }
    }
}
