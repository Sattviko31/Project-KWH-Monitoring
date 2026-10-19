using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace KWHMonitoring.Migrations
{
    public partial class AddBillingConfirmationRecipientsAndNotes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AnomalyLogId",
                table: "BillingConfirmationTokens",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "BillingConfirmationTokens",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                table: "ApplicationUsers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BillingAnomalyNotes",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    AnomalyLogId = table.Column<long>(nullable: false),
                    UserId = table.Column<int>(nullable: true),
                    Author = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NoteType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Decision = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingAnomalyNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillingConfirmationRecipients",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(nullable: false),
                    IsSelected = table.Column<bool>(nullable: false),
                    SelectedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SelectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingConfirmationRecipients", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingAnomalyNotes_Anomaly_Created",
                table: "BillingAnomalyNotes",
                columns: new[] { "AnomalyLogId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingConfirmationRecipients_UserId",
                table: "BillingConfirmationRecipients",
                column: "UserId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingAnomalyNotes");

            migrationBuilder.DropTable(
                name: "BillingConfirmationRecipients");

            migrationBuilder.DropColumn(
                name: "AnomalyLogId",
                table: "BillingConfirmationTokens");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "BillingConfirmationTokens");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                table: "ApplicationUsers");
        }
    }
}
