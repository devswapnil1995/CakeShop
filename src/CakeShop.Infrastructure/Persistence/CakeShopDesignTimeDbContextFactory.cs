using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CakeShop.Infrastructure.Persistence;

/// <summary>Allows EF tooling to create migrations without booting the HTTP host or requiring JWT settings.</summary>
public sealed class CakeShopDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CakeShopDbContext>
{
    public CakeShopDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CAKESHOP_SQL_CONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Set CAKESHOP_SQL_CONNECTION or ConnectionStrings__DefaultConnection before running EF migrations.");

        var options = new DbContextOptionsBuilder<CakeShopDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new CakeShopDbContext(options);
    }
}
