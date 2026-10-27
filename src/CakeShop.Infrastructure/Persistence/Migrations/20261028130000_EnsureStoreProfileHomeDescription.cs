using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeShop.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(CakeShopDbContext))]
    [Migration("20261028130000_EnsureStoreProfileHomeDescription")]
    public partial class EnsureStoreProfileHomeDescription : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddStoreProfile may already have been applied before HomeDescription
            // was introduced. Guard this repair so it is safe on both old and fresh databases.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[StoreProfiles]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.StoreProfiles', N'HomeDescription') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[StoreProfiles]
                    ADD [HomeDescription] nvarchar(320) NOT NULL
                        CONSTRAINT [DF_StoreProfiles_HomeDescription]
                        DEFAULT N'Thoughtfully baked, beautifully shared. Discover the care and creativity behind every Sweet Panda creation.';
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep this repair reversible without assuming every environment had the column.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[StoreProfiles]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.StoreProfiles', N'HomeDescription') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[StoreProfiles]
                    DROP CONSTRAINT [DF_StoreProfiles_HomeDescription];
                    ALTER TABLE [dbo].[StoreProfiles] DROP COLUMN [HomeDescription];
                END
                """);
        }
    }
}
