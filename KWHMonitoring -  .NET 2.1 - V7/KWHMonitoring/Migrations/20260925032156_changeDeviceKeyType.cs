using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class changeDeviceKeyType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RelayControl_DeviceKey' AND object_id = OBJECT_ID('RelayControl'))
                BEGIN
                    DROP INDEX [IX_RelayControl_DeviceKey] ON [RelayControl];
                END
            ");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceKey",
                table: "RelayControl",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RelayControl_DeviceKey' AND object_id = OBJECT_ID('RelayControl'))
                BEGIN
                    CREATE NONCLUSTERED INDEX [IX_RelayControl_DeviceKey] ON [RelayControl]([DeviceKey] ASC);
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RelayControl_DeviceKey' AND object_id = OBJECT_ID('RelayControl'))
                BEGIN
                    DROP INDEX [IX_RelayControl_DeviceKey] ON [RelayControl];
                END
            ");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceKey",
                table: "RelayControl",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RelayControl_DeviceKey' AND object_id = OBJECT_ID('RelayControl'))
                BEGIN
                    CREATE NONCLUSTERED INDEX [IX_RelayControl_DeviceKey] ON [RelayControl]([DeviceKey] ASC);
                END
            ");
        }
    }
}
