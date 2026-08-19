using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class MigrationInitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ═══════════════════════════════════════════════
            // 1. DeviceRegistry
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "DeviceRegistry",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    DeviceId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    GroupName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    FirstSeen = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                    MessageCount = table.Column<long>(nullable: false, defaultValue: 0L),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceRegistry", x => x.Id);
                    table.UniqueConstraint("AK_DeviceRegistry_DeviceKey", x => x.DeviceKey);
                    table.UniqueConstraint("AK_DeviceRegistry_DeviceId", x => x.DeviceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistry_DeviceId",
                table: "DeviceRegistry",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistry_DeviceKey",
                table: "DeviceRegistry",
                column: "DeviceKey");

            // ═══════════════════════════════════════════════
            // 2. KWHData
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "KWHData",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    TerminalTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedTime = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()"),
                    GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PHASE_R = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PHASE_S = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PHASE_T = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AMPERE_R = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    AMPERE_S = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    AMPERE_T = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    W = table.Column<decimal>(type: "decimal(18,1)", nullable: true),
                    CosPhi = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    F = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Aktif_Power = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalW = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalW1M = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KWHData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KWHData_DeviceRegistry",
                        column: x => x.DeviceKey,
                        principalTable: "DeviceRegistry",
                        principalColumn: "DeviceKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KWHData_DeviceKey",
                table: "KWHData",
                column: "DeviceKey");

            migrationBuilder.CreateIndex(
                name: "IX_KWHData_DeviceKey_Only",
                table: "KWHData",
                column: "DeviceKey");

            migrationBuilder.CreateIndex(
                name: "IX_KWHData_ReceivedTime",
                table: "KWHData",
                column: "ReceivedTime");

            migrationBuilder.CreateIndex(
                name: "IX_KWHData_TerminalTime",
                table: "KWHData",
                column: "TerminalTime");

            migrationBuilder.CreateIndex(
                name: "IX_KWHData_DeviceKey_TerminalTime",
                table: "KWHData",
                columns: new[] { "DeviceKey", "TerminalTime" });

            // Indexes with INCLUDE / DESC require raw SQL
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_KWHData_DeviceKey_ReceivedTime]
                ON [dbo].[KWHData] ([DeviceKey] ASC, [ReceivedTime] DESC)
                INCLUDE ([DeviceId],[GroupName],[TerminalTime],[PHASE_R],[PHASE_S],[PHASE_T],
                         [AMPERE_R],[AMPERE_S],[AMPERE_T],[CosPhi],[W],[TotalW1M],[Aktif_Power],[TotalW],[F]);
            ");

            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_KWHData_ReceivedTime_DeviceKey]
                ON [dbo].[KWHData] ([ReceivedTime] DESC, [DeviceKey] ASC)
                INCLUDE ([DeviceId],[GroupName],[TerminalTime],[PHASE_R],[PHASE_S],[PHASE_T],
                         [AMPERE_R],[AMPERE_S],[AMPERE_T],[CosPhi],[W],[TotalW1M],[Aktif_Power],[TotalW],[F]);
            ");

            // ═══════════════════════════════════════════════
            // 3. AnomalyLogs
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "AnomalyLogs",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AnomalyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PowerValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ThresholdValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Deviation = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DetectedTime = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()"),
                    EMAValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ThresholdMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, defaultValue: "manual"),
                    Acknowledged = table.Column<bool>(nullable: true, defaultValue: false),
                    AcknowledgedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnomalyLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyLogs_DetectedTime",
                table: "AnomalyLogs",
                column: "DetectedTime");

            migrationBuilder.CreateIndex(
                name: "IX_AnomalyLogs_DeviceKey",
                table: "AnomalyLogs",
                column: "DeviceKey");

            // ═══════════════════════════════════════════════
            // 4. AppLog
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "AppLog",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    LogLevel = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Topic = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    DeviceKey = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLog", x => x.Id);
                });

            // DESC index requires raw SQL
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_AppLog_CreatedAt]
                ON [dbo].[AppLog] ([CreatedAt] DESC);
            ");

            migrationBuilder.CreateIndex(
                name: "IX_AppLog_Level",
                table: "AppLog",
                column: "LogLevel");

            // ═══════════════════════════════════════════════
            // 5. AppSettings
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    SettingKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "getdate()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_SettingKey",
                table: "AppSettings",
                column: "SettingKey",
                unique: true);

            // ═══════════════════════════════════════════════
            // 6. ColumnMapping
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "ColumnMapping",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    OldColumnName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    NewColumnName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColumnMapping", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColumnMapping_OldName",
                table: "ColumnMapping",
                column: "OldColumnName",
                unique: true);

            // ═══════════════════════════════════════════════
            // 7. ColumnScaleConfig
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "ColumnScaleConfig",
                columns: table => new
                {
                    ColumnName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    ScaleFactor = table.Column<decimal>(type: "decimal(18,5)", nullable: false),
                    RegisterAddress = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true),
                    DataType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "DECIMAL(18,3)"),
                    Unit = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Category = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    IsDynamic = table.Column<bool>(nullable: false, defaultValue: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColumnScaleConfig", x => x.ColumnName);
                });

            // ═══════════════════════════════════════════════
            // 8. DailyEnergy
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "DailyEnergy",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    EnergyKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyEnergy", x => x.Id);
                });

            // ═══════════════════════════════════════════════
            // 9. FailedMessages
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "FailedMessages",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Topic = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetryCount = table.Column<int>(nullable: false, defaultValue: 0),
                    IsResolved = table.Column<bool>(nullable: false, defaultValue: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()"),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_IsResolved",
                table: "FailedMessages",
                column: "IsResolved");

            // DESC index requires raw SQL
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_FailedMessages_ReceivedAt]
                ON [dbo].[FailedMessages] ([ReceivedAt] DESC);
            ");

            // ═══════════════════════════════════════════════
            // 10. HourlyEnergy
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "HourlyEnergy",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Hour = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnergyKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HourlyEnergy", x => x.Id);
                });

            // ═══════════════════════════════════════════════
            // 11. KWHData_History
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "KWHData_History",
                columns: table => new
                {
                    HistoryId = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    OriginalId = table.Column<long>(nullable: false),
                    DeviceKey = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    TerminalTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PHASE_R = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PHASE_S = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PHASE_T = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AMPERE_R = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    AMPERE_S = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    AMPERE_T = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    W = table.Column<decimal>(type: "decimal(18,1)", nullable: true),
                    CosPhi = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    F = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Aktif_Power = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalW = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalW1M = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KWHData_History", x => x.HistoryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KWHData_History_DeviceKey",
                table: "KWHData_History",
                column: "DeviceKey");

            // DESC index requires raw SQL
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_KWHData_History_ArchivedAt]
                ON [dbo].[KWHData_History] ([ArchivedAt] DESC);
            ");

            // ═══════════════════════════════════════════════
            // 12. MonthlyEnergy
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "MonthlyEnergy",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(nullable: false),
                    Month = table.Column<int>(nullable: false),
                    EnergyKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyEnergy", x => x.Id);
                });

            // ═══════════════════════════════════════════════
            // 13. YearlyEnergy
            // ═══════════════════════════════════════════════
            migrationBuilder.CreateTable(
                name: "YearlyEnergy",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DeviceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(nullable: false),
                    EnergyKWh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "getdate()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyEnergy", x => x.Id);
                });

            // ═══════════════════════════════════════════════
            // 14. Views
            // ═══════════════════════════════════════════════
            migrationBuilder.Sql(@"
                CREATE VIEW [dbo].[vLatestKWHData]
                AS
                SELECT 
                    k.Id, k.DeviceKey, d.DeviceId, d.GroupName,
                    k.TerminalTime, k.ReceivedTime,
                    k.PHASE_R, k.PHASE_S, k.PHASE_T,
                    k.AMPERE_R, k.AMPERE_S, k.AMPERE_T,
                    k.CosPhi, k.W, k.Aktif_Power, k.TotalW, k.TotalW1M, k.F
                FROM KWHData k
                INNER JOIN DeviceRegistry d ON k.DeviceKey = d.DeviceKey
                INNER JOIN (
                    SELECT DeviceKey, MAX(ReceivedTime) AS MaxTime
                    FROM KWHData GROUP BY DeviceKey
                ) latest ON k.DeviceKey = latest.DeviceKey AND k.ReceivedTime = latest.MaxTime;
            ");

            migrationBuilder.Sql(@"
                CREATE VIEW [dbo].[vDeviceSummary]
                AS
                SELECT 
                    d.DeviceKey, d.DeviceId, d.GroupName,
                    d.FirstSeen, d.LastSeen, d.IsActive, d.MessageCount,
                    COUNT(k.Id) AS TotalRecords,
                    MAX(k.ReceivedTime) AS LastDataReceived
                FROM DeviceRegistry d
                LEFT JOIN KWHData k ON d.DeviceKey = k.DeviceKey
                GROUP BY d.DeviceKey, d.DeviceId, d.GroupName,
                         d.FirstSeen, d.LastSeen, d.IsActive, d.MessageCount;
            ");

            migrationBuilder.Sql(@"
                CREATE VIEW [dbo].[vDailyEnergy]
                AS
                SELECT 
                    k.DeviceKey, d.GroupName,
                    CAST(k.TerminalTime AS DATE) AS ReportDate,
                    MIN(k.TotalW) AS EnergyStart_kWh,
                    MAX(k.TotalW) AS EnergyEnd_kWh,
                    (MAX(k.TotalW) - MIN(k.TotalW)) AS DailyConsumption_kWh,
                    COUNT(*) AS ReadingCount
                FROM KWHData k
                INNER JOIN DeviceRegistry d ON k.DeviceKey = d.DeviceKey
                GROUP BY k.DeviceKey, d.GroupName, CAST(k.TerminalTime AS DATE);
            ");

            // ═══════════════════════════════════════════════
            // 15. Stored Procedures
            // ═══════════════════════════════════════════════
            migrationBuilder.Sql(@"
                CREATE PROCEDURE [dbo].[sp_CleanupOldData]
                    @DaysToKeep INT = 90
                AS
                BEGIN
                    SET NOCOUNT ON;
                    DELETE FROM KWHData WHERE ReceivedTime < DATEADD(DAY, -@DaysToKeep, GETDATE());
                    DELETE FROM AppLog WHERE CreatedAt < DATEADD(DAY, -@DaysToKeep, GETDATE());
                    DELETE FROM FailedMessages WHERE ReceivedAt < DATEADD(DAY, -30, GETDATE()) AND IsResolved = 1;
                END;
            ");

            migrationBuilder.Sql(@"
                CREATE PROCEDURE [dbo].[sp_RegisterDevice]
                    @DeviceId VARCHAR(50),
                    @GroupName VARCHAR(100) = NULL,
                    @DeviceKey VARCHAR(20) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF NOT EXISTS (SELECT 1 FROM DeviceRegistry WHERE DeviceId = @DeviceId)
                    BEGIN
                        DECLARE @NextNumber INT;
                        SELECT @NextNumber = ISNULL(MAX(CAST(SUBSTRING(DeviceKey, 5, 3) AS INT)), 0) + 1
                        FROM DeviceRegistry WHERE DeviceKey LIKE 'KWH-%';
                        SET @DeviceKey = 'KWH-' + RIGHT('000' + CAST(@NextNumber AS VARCHAR), 3);
                        INSERT INTO DeviceRegistry (DeviceKey, DeviceId, GroupName, FirstSeen, LastSeen)
                        VALUES (@DeviceKey, @DeviceId, @GroupName, GETDATE(), GETDATE());
                    END
                    ELSE
                    BEGIN
                        SELECT @DeviceKey = DeviceKey FROM DeviceRegistry WHERE DeviceId = @DeviceId;
                        UPDATE DeviceRegistry 
                        SET LastSeen = GETDATE(), GroupName = ISNULL(@GroupName, GroupName), UpdatedAt = GETDATE()
                        WHERE DeviceId = @DeviceId;
                    END
                END;
            ");

            // ═══════════════════════════════════════════════
            // 16. Database User & Roles
            // ═══════════════════════════════════════════════
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'kwhapp')
                BEGIN
                    CREATE USER [kwhapp] WITHOUT LOGIN WITH DEFAULT_SCHEMA=[dbo];
                    ALTER ROLE [db_ddladmin] ADD MEMBER [kwhapp];
                    ALTER ROLE [db_datareader] ADD MEMBER [kwhapp];
                    ALTER ROLE [db_datawriter] ADD MEMBER [kwhapp];
                END;
            ");

            // ═══════════════════════════════════════════════
            // 17. Seed AppSettings
            // ═══════════════════════════════════════════════
            migrationBuilder.Sql(@"
                INSERT INTO [AppSettings] ([SettingKey], [SettingValue], [UpdatedAt]) VALUES
                ('MaxCapacity', '100000', GETDATE()),
                ('NotificationEnabled', 'false', GETDATE()),
                ('CheckIntervalSeconds', '30', GETDATE());
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [dbo].[sp_RegisterDevice]");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [dbo].[sp_CleanupOldData]");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[vDailyEnergy]");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[vDeviceSummary]");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[vLatestKWHData]");

            migrationBuilder.DropTable(name: "YearlyEnergy");
            migrationBuilder.DropTable(name: "MonthlyEnergy");
            migrationBuilder.DropTable(name: "DailyEnergy");
            migrationBuilder.DropTable(name: "HourlyEnergy");
            migrationBuilder.DropTable(name: "FailedMessages");
            migrationBuilder.DropTable(name: "ColumnScaleConfig");
            migrationBuilder.DropTable(name: "ColumnMapping");
            migrationBuilder.DropTable(name: "AppSettings");
            migrationBuilder.DropTable(name: "AppLog");
            migrationBuilder.DropTable(name: "AnomalyLogs");
            migrationBuilder.DropTable(name: "KWHData_History");
            migrationBuilder.DropTable(name: "KWHData");
            migrationBuilder.DropTable(name: "DeviceRegistry");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'kwhapp')
                BEGIN
                    ALTER ROLE [db_datawriter] DROP MEMBER [kwhapp];
                    ALTER ROLE [db_datareader] DROP MEMBER [kwhapp];
                    ALTER ROLE [db_ddladmin] DROP MEMBER [kwhapp];
                    DROP USER [kwhapp];
                END;
            ");
        }
    }
}
