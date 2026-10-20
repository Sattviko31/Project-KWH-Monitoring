using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddBillingSnapshotLocationFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupName",
                table: "BillingEstimateSnapshots",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KodeLokasi",
                table: "BillingEstimateSnapshots",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IDPelanggan",
                table: "BillingEstimateSnapshots",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupName",
                table: "BillingEstimateSnapshots");

            migrationBuilder.DropColumn(
                name: "KodeLokasi",
                table: "BillingEstimateSnapshots");

            migrationBuilder.DropColumn(
                name: "IDPelanggan",
                table: "BillingEstimateSnapshots");
        }
    }
}
