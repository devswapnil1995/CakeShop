using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProviderOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderPaymentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_ClientMaster_ClientId",
                        column: x => x.ClientId,
                        principalTable: "ClientMaster",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_CustomerOrders_ClientId_OrderId",
                        columns: x => new { x.ClientId, x.OrderId },
                        principalTable: "CustomerOrders",
                        principalColumns: new[] { "ClientId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantPaymentSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MerchantAccountReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    KeyId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CredentialSecretReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPaymentSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantPaymentSettings_ClientMaster_ClientId",
                        column: x => x.ClientId,
                        principalTable: "ClientMaster",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ClientId_OrderId",
                table: "PaymentTransactions",
                columns: new[] { "ClientId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Provider_ProviderOrderId",
                table: "PaymentTransactions",
                columns: new[] { "Provider", "ProviderOrderId" },
                unique: true,
                filter: "[ProviderOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Provider_ProviderPaymentId",
                table: "PaymentTransactions",
                columns: new[] { "Provider", "ProviderPaymentId" },
                unique: true,
                filter: "[ProviderPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPaymentSettings_ClientId_Provider_Environment",
                table: "TenantPaymentSettings",
                columns: new[] { "ClientId", "Provider", "Environment" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "TenantPaymentSettings");
        }
    }
}
