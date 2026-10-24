using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeShop.Infrastructure.Persistence.Migrations
{
    public partial class AddHeroBanners : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeroBanners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MediaUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MobileMediaUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ButtonText = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ButtonUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    StartsUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndsUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeroBanners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeroBanners_ClientMaster_ClientId",
                        column: x => x.ClientId,
                        principalTable: "ClientMaster",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeroBanners_ClientId_SortOrder",
                table: "HeroBanners",
                columns: new[] { "ClientId", "SortOrder" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "HeroBanners");
        }
    }
}