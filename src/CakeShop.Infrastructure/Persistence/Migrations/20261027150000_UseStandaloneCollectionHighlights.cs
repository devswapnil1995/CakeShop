using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using CakeShop.Infrastructure.Persistence;

#nullable disable

namespace CakeShop.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(CakeShopDbContext))]
    [Migration("20261027150000_UseStandaloneCollectionHighlights")]
    public partial class UseStandaloneCollectionHighlights : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Highlights",
                table: "StoreCollections",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.DropTable(
                name: "StoreCollectionItems");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoreCollectionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreCollectionItems", x => x.Id);
                    table.ForeignKey("FK_StoreCollectionItems_StoreCollections_ClientId_CollectionId", x => new { x.ClientId, x.CollectionId }, "StoreCollections", new[] { "ClientId", "Id" }, onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_StoreCollectionItems_Products_ClientId_ProductId", x => new { x.ClientId, x.ProductId }, "Products", new[] { "ClientId", "Id" }, onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_StoreCollectionItems_ClientId_CollectionId_SortOrder", "StoreCollectionItems", new[] { "ClientId", "CollectionId", "SortOrder" });
            migrationBuilder.CreateIndex("IX_StoreCollectionItems_ClientId_ProductId", "StoreCollectionItems", new[] { "ClientId", "ProductId" });

            migrationBuilder.DropColumn(name: "Highlights", table: "StoreCollections");
        }
    }
}
