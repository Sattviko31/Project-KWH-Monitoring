using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddIsMasterAdmin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMasterAdmin",
                table: "ApplicationUsers",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_IsMasterAdmin",
                table: "ApplicationUsers",
                column: "IsMasterAdmin");

            // Backfill: set IsMasterAdmin = true for the user whose email matches
            // the current Notification.MasterAdminEmail setting (if it exists)
            migrationBuilder.Sql(
                @"UPDATE AU
                  SET AU.IsMasterAdmin = 1
                  FROM ApplicationUsers AU
                  INNER JOIN AppSettings AR ON AR.SettingKey = 'Notification.MasterAdminEmail'
                  WHERE AU.Email = AR.SettingValue
                    AND AU.IsActive = 1");

            // If no AppSettings record exists but a seeded admin exists (first admin by Id),
            // promote the earliest active admin as master admin
            migrationBuilder.Sql(
                @"UPDATE ApplicationUsers
                  SET IsMasterAdmin = 1
                  WHERE Id = (
                      SELECT MIN(Id) FROM ApplicationUsers
                      WHERE Role = 'Admin' AND IsActive = 1
                        AND NOT EXISTS (
                            SELECT 1 FROM ApplicationUsers WHERE IsMasterAdmin = 1
                        )
                  )");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_IsMasterAdmin",
                table: "ApplicationUsers");

            migrationBuilder.DropColumn(
                name: "IsMasterAdmin",
                table: "ApplicationUsers");
        }
    }
}
