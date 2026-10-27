using System;
using CakeShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeShop.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(CakeShopDbContext))]
    [Migration("20261026130000_AddCartOrderItems")]
    public partial class AddCartOrderItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProductVariants_ClientId_Id",
                table: "ProductVariants",
                columns: new[] { "ClientId", "Id" });

            migrationBuilder.CreateTable(
                name: "CustomerOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SizeLabel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    WeightInGrams = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerOrderItems_CustomerOrders_ClientId_OrderId",
                        columns: x => new { x.ClientId, x.OrderId },
                        principalTable: "CustomerOrders",
                        principalColumns: new[] { "ClientId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerOrderItems_Products_ClientId_ProductId",
                        columns: x => new { x.ClientId, x.ProductId },
                        principalTable: "Products",
                        principalColumns: new[] { "ClientId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerOrderItems_ProductVariants_ClientId_ProductVariantId",
                        columns: x => new { x.ClientId, x.ProductVariantId },
                        principalTable: "ProductVariants",
                        principalColumns: new[] { "ClientId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderItems_ClientId_OrderId",
                table: "CustomerOrderItems",
                columns: new[] { "ClientId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderItems_ClientId_ProductId",
                table: "CustomerOrderItems",
                columns: new[] { "ClientId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderItems_ClientId_ProductVariantId",
                table: "CustomerOrderItems",
                columns: new[] { "ClientId", "ProductVariantId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CustomerOrderItems");
            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProductVariants_ClientId_Id",
                table: "ProductVariants");
        }
    }
}
