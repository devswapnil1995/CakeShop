using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace CakeShop.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(CakeShopDbContext))]
    [Migration("20261027100000_AddStoreCollections")]
    public partial class AddStoreCollections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoreCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NavigationLabel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ShowInNavigation = table.Column<bool>(type: "bit", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    StartsUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndsUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreCollections", x => x.Id);
                    table.ForeignKey("FK_StoreCollections_ClientMaster_ClientId", x => x.ClientId, "ClientMaster", "Id", onDelete: ReferentialAction.Restrict);
                    table.UniqueConstraint("AK_StoreCollections_ClientId_Id", x => new { x.ClientId, x.Id });
                });

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

            migrationBuilder.CreateIndex("IX_StoreCollections_ClientId_Kind_Slug", "StoreCollections", new[] { "ClientId", "Kind", "Slug" }, unique: true);
            migrationBuilder.CreateIndex("IX_StoreCollections_ClientId_IsPublished_SortOrder", "StoreCollections", new[] { "ClientId", "IsPublished", "SortOrder" });
            migrationBuilder.CreateIndex("IX_StoreCollectionItems_ClientId_CollectionId_SortOrder", "StoreCollectionItems", new[] { "ClientId", "CollectionId", "SortOrder" });
            migrationBuilder.CreateIndex("IX_StoreCollectionItems_ClientId_ProductId", "StoreCollectionItems", new[] { "ClientId", "ProductId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("StoreCollectionItems");
            migrationBuilder.DropTable("StoreCollections");
        }
    }
}