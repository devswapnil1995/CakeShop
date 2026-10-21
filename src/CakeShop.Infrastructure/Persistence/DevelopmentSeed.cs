using CakeShop.Domain.Catalog;
using CakeShop.Domain.Expenses;
using CakeShop.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace CakeShop.Infrastructure.Persistence;

/// <summary>Explicit, repeatable local seed. Run only after EF migrations have been applied.</summary>
public static class DevelopmentSeed
{
    public static readonly Guid DefaultClientId = Guid.Parse("c8ef8d24-5d9b-4e2e-91e2-79f4b0e9a001");

    public static async Task SeedAsync(CakeShopDbContext db, CancellationToken ct = default)
    {
        if (!await db.Clients.AnyAsync(x => x.Id == DefaultClientId, ct))
            db.Clients.Add(new ClientMaster(DefaultClientId, "The Sweet Panda", "the-sweet-panda"));       
        await db.SaveChangesAsync(ct);


        //--delete from ProductVariants;
        //--delete from OrderStatusHistories;
        //--delete from CustomerOrders;
        //--delete from Products;
        //--delete from Categories;
        //--delete from Expense
    }
}
