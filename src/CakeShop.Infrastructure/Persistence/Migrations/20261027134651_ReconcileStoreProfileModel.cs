using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeShop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileStoreProfileModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
        IF OBJECT_ID(N'dbo.StoreCollections', N'U') IS NOT NULL
           AND OBJECT_ID(
               N'dbo.FK_StoreCollections_ClientMaster_ClientId',
               N'F'
           ) IS NULL
        BEGIN
            ALTER TABLE dbo.StoreCollections
            ADD CONSTRAINT FK_StoreCollections_ClientMaster_ClientId
            FOREIGN KEY (ClientId)
            REFERENCES dbo.ClientMaster (Id)
            ON DELETE NO ACTION;
        END
        """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoreCollections_ClientMaster_ClientId",
                table: "StoreCollections");
        }
    }
}
