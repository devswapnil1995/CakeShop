using CakeShop.Domain.Catalog;
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
        var categories = new[] {
            (Id: Guid.Parse("a1000000-0000-4000-8000-000000000001"), Name: "Celebration", Slug: "celebration", Sort: 1),
            (Id: Guid.Parse("a1000000-0000-4000-8000-000000000002"), Name: "Chocolate", Slug: "chocolate", Sort: 2),
            (Id: Guid.Parse("a1000000-0000-4000-8000-000000000003"), Name: "Signature", Slug: "signature", Sort: 3)
        };
        foreach (var item in categories)
            if (!await db.Categories.AnyAsync(x => x.ClientId == DefaultClientId && x.Slug == item.Slug, ct))
                db.Categories.Add(new Category(item.Id, DefaultClientId, item.Name, item.Slug, item.Sort));
        await db.SaveChangesAsync(ct);
        var products = new[] {
            (Id: Guid.Parse("b1000000-0000-4000-8000-000000000001"), Cat: categories[0].Id, Name: "Velvet Rose Cake", Slug: "velvet-rose-cake", Desc: "Silky vanilla cream, soft sponge and delicate rose finish.", Price: 799m, Image: "https://images.unsplash.com/photo-1578985545062-69928b1d9587?auto=format&fit=crop&w=900&q=85", Badge: "Bestseller", Featured: true),
            (Id: Guid.Parse("b1000000-0000-4000-8000-000000000002"), Cat: categories[1].Id, Name: "Midnight Chocolate", Slug: "midnight-chocolate", Desc: "Deep cocoa layers with a glossy chocolate ganache.", Price: 899m, Image: "https://images.unsplash.com/photo-1606313564200-e75d5e30476c?auto=format&fit=crop&w=900&q=85", Badge: "Chocolate lover’s pick", Featured: true),
            (Id: Guid.Parse("b1000000-0000-4000-8000-000000000003"), Cat: categories[2].Id, Name: "Berry Cloud Cake", Slug: "berry-cloud-cake", Desc: "Light whipped cream, seasonal berries and soft vanilla.", Price: 849m, Image: "https://images.unsplash.com/photo-1565958011703-44f9829ba187?auto=format&fit=crop&w=900&q=85", Badge: null, Featured: true)
        };
        foreach (var item in products)
            if (!await db.Products.AnyAsync(x => x.ClientId == DefaultClientId && x.Slug == item.Slug, ct))
                db.Products.Add(new Product(item.Id, DefaultClientId, item.Cat, item.Name, item.Slug, item.Desc, item.Price, item.Image, item.Badge, item.Featured));
        await db.SaveChangesAsync(ct);
    }
}
