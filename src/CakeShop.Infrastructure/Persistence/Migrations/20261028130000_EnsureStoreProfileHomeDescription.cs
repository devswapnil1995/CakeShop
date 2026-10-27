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
            // Repair databases where AddStoreProfile was recorded before HomeDescription
            // was added to the model. On fresh databases the column already exists, so this
            // migration deliberately becomes a no-op.
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
            // Intentionally do not drop HomeDescription. On a fresh database it belongs to
            // AddStoreProfile; removing it here would make rollback destructive. This is a
            // forward-only compatibility repair for databases with schema drift.
        }
    }
}
