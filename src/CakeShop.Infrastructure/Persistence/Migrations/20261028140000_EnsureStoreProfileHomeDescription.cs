using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeShop.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(CakeShopDbContext))]
    [Migration("20261028140000_EnsureStoreProfileHomeDescription")]
    public partial class EnsureStoreProfileHomeDescription : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some local databases applied AddStoreProfile before HomeDescription was introduced.
            // Keep this repair safe for both those databases and fresh installs.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.StoreProfiles', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.StoreProfiles', N'HomeDescription') IS NULL
                BEGIN
                    ALTER TABLE dbo.StoreProfiles
                    ADD HomeDescription nvarchar(320) NOT NULL
                        CONSTRAINT DF_StoreProfiles_HomeDescription
                        DEFAULT N'Thoughtfully baked, beautifully shared. Discover the care and creativity behind every Sweet Panda creation.';
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally do not drop HomeDescription: it is part of the current StoreProfile model
            // and may have been created by the original AddStoreProfile migration on fresh databases.
        }
    }
}
