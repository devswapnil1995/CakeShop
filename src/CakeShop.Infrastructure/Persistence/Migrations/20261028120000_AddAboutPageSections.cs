using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace CakeShop.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(CakeShopDbContext))]
    [Migration("20261028120000_AddAboutPageSections")]
    public partial class AddAboutPageSections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AboutPageSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Eyebrow = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SecondaryImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Layout = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Animation = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AboutPageSections", x => x.Id);
                    table.ForeignKey("FK_AboutPageSections_ClientMaster_ClientId", x => x.ClientId, "ClientMaster", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "IX_AboutPageSections_ClientId_SortOrder", table: "AboutPageSections", columns: new[] { "ClientId", "SortOrder" });
        }

        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "AboutPageSections");
    }
}
