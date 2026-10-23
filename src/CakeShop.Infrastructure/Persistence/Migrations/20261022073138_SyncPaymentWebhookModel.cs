using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncPaymentWebhookModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedPaymentWebhooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProcessedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedPaymentWebhooks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedPaymentWebhooks_Provider_EventId",
                table: "ProcessedPaymentWebhooks",
                columns: new[] { "Provider", "EventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedPaymentWebhooks");
        }
    }
}
