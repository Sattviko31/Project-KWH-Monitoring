using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddIsMasterAdmin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Buat tabel RelayControl dengan tipe Id (bigint) & RCI (int) jika belum ada
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[RelayControl]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[RelayControl] (
                        [Id] bigint IDENTITY(1,1) NOT NULL,
                        [DeviceKey] nvarchar(100) NULL,
                        [RCI] int NULL,
                        [DeviceId] nvarchar(50) NULL,
                        [GroupName] nvarchar(100) NULL,
                        [ReceivedTime] datetime2 NOT NULL,
                        CONSTRAINT [PK_RelayControl] PRIMARY KEY CLUSTERED ([Id] ASC)
                    );
                END
            ");

            // 2. Tambah kolom IsMasterAdmin pada ApplicationUsers
            migrationBuilder.AddColumn<bool>(
                name: "IsMasterAdmin",
                table: "ApplicationUsers",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_IsMasterAdmin",
                table: "ApplicationUsers",
                column: "IsMasterAdmin");

            // 3. Backfill data IsMasterAdmin (dari logic migration sebelumnya)
            migrationBuilder.Sql(@"
                UPDATE AU
                SET AU.IsMasterAdmin = 1
                FROM ApplicationUsers AU
                INNER JOIN AppSettings AR ON AR.SettingKey = 'Notification.MasterAdminEmail'
                WHERE AU.Email = AR.SettingValue
                  AND AU.IsActive = 1;

                UPDATE ApplicationUsers
                SET IsMasterAdmin = 1
                WHERE Id = (
                    SELECT MIN(Id) FROM ApplicationUsers
                    WHERE Role = 'Admin' AND IsActive = 1
                      AND NOT EXISTS (
                          SELECT 1 FROM ApplicationUsers WHERE IsMasterAdmin = 1
                      )
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_IsMasterAdmin",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "IsMasterAdmin",
                table: "ApplicationUsers");

            // Rollback RelayControl
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[RelayControl]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [dbo].[RelayControl];
                END
            ");
        }
    }
}