using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace CakeShop.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(CakeShopDbContext))]
    [Migration("20261028100000_AddStoreProfile")]
    public partial class AddStoreProfile : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoreProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tagline = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    StoryTitle = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Story = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    StoryImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    WhatsAppNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OpeningHours = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InstagramUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FacebookUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreProfiles", x => x.Id);
                    table.ForeignKey("FK_StoreProfiles_ClientMaster_ClientId", x => x.ClientId, "ClientMaster", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "IX_StoreProfiles_ClientId", table: "StoreProfiles", column: "ClientId", unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "StoreProfiles");
        }
    }
}
