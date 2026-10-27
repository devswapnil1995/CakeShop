using CakeShop.Domain.Catalog;
using CakeShop.Domain.Content;
using CakeShop.Domain.Expenses;
using CakeShop.Domain.Identity;
using CakeShop.Domain.Orders;
using CakeShop.Domain.Payments;
using CakeShop.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace CakeShop.Infrastructure.Persistence;

public sealed class CakeShopDbContext(DbContextOptions<CakeShopDbContext> options) : DbContext(options)
{
    public DbSet<ClientMaster> Clients => Set<ClientMaster>();
    public DbSet<HeroBanner> HeroBanners => Set<HeroBanner>();
    public DbSet<StoreCollection> StoreCollections => Set<StoreCollection>();
    public DbSet<StoreProfile> StoreProfiles => Set<StoreProfile>();
    public DbSet<AboutPageSection> AboutPageSections => Set<AboutPageSection>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();
    public DbSet<CustomerOrderItem> CustomerOrderItems => Set<CustomerOrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories
    => Set<OrderStatusHistory>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<PaymentTransaction> PaymentTransactions
    => Set<PaymentTransaction>();
    public DbSet<TenantPaymentSettings> TenantPaymentSettings
    => Set<TenantPaymentSettings>();

    public DbSet<ProcessedPaymentWebhook> ProcessedPaymentWebhooks
    => Set<ProcessedPaymentWebhook>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AboutPageSection>(e =>
        {
            e.ToTable("AboutPageSections");
            e.HasKey(x => x.Id);
            e.Property(x => x.Eyebrow).HasMaxLength(100).IsRequired();
            e.Property(x => x.Title).HasMaxLength(180).IsRequired();
            e.Property(x => x.Body).HasMaxLength(3000).IsRequired();
            e.Property(x => x.ImageUrl).HasMaxLength(1000);
            e.Property(x => x.SecondaryImageUrl).HasMaxLength(1000);
            e.Property(x => x.Layout).HasMaxLength(24).IsRequired();
            e.Property(x => x.Animation).HasMaxLength(24).IsRequired();
            e.HasIndex(x => new { x.ClientId, x.SortOrder });
            e.HasOne<ClientMaster>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StoreProfile>(e =>
        {
            e.ToTable("StoreProfiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tagline).HasMaxLength(120).IsRequired();
            e.Property(x => x.StoryTitle).HasMaxLength(180).IsRequired();
            e.Property(x => x.Story).HasMaxLength(3000).IsRequired();
            e.Property(x => x.StoryImageUrl).HasMaxLength(1000);
            e.Property(x => x.LogoUrl).HasMaxLength(1000);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.WhatsAppNumber).HasMaxLength(40);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.OpeningHours).HasMaxLength(500);
            e.Property(x => x.InstagramUrl).HasMaxLength(500);
            e.Property(x => x.FacebookUrl).HasMaxLength(500);
            e.Property(x => x.WebsiteUrl).HasMaxLength(500);
            e.HasIndex(x => x.ClientId).IsUnique();
            e.HasOne<ClientMaster>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StoreCollection>(e =>
        {
            e.ToTable("StoreCollections");
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.ClientId, x.Id });
            e.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(180).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Highlights).HasMaxLength(4000).IsRequired();
            e.Property(x => x.ImageUrl).HasMaxLength(1000);
            e.Property(x => x.NavigationLabel).HasMaxLength(60);
            e.Property(x => x.BundlePrice).HasColumnType("decimal(18,2)");
            e.HasIndex(x => new { x.ClientId, x.Kind, x.Slug }).IsUnique();
            e.HasIndex(x => new { x.ClientId, x.IsPublished, x.SortOrder });
            e.HasOne<ClientMaster>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HeroBanner>(e =>
        {
            e.ToTable("HeroBanners");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.Subtitle).HasMaxLength(500).IsRequired();
            e.Property(x => x.TextPosition).HasMaxLength(20).IsRequired();
            e.Property(x => x.MediaType).HasMaxLength(20).IsRequired();
            e.Property(x => x.MediaUrl).HasMaxLength(2000).IsRequired();
            e.Property(x => x.MobileMediaUrl).HasMaxLength(2000);
            e.Property(x => x.ButtonText).HasMaxLength(80);
            e.Property(x => x.ButtonUrl).HasMaxLength(2000);
            e.HasIndex(x => new { x.ClientId, x.SortOrder });
            e.HasOne<ClientMaster>().WithMany().HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClientMaster>(e => {
            e.ToTable("ClientMaster"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(100).IsRequired(); e.HasIndex(x => x.Slug).IsUnique();
        });
        modelBuilder.Entity<AdminUser>(e => {
            e.ToTable("AdminUsers"); e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.NormalizedEmail).HasMaxLength(254).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Role).HasMaxLength(40).IsRequired();
            e.HasIndex(x => new { x.ClientId, x.NormalizedEmail }).IsUnique();
            e.HasOne<ClientMaster>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Category>(e => {
            e.ToTable("Categories"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(120).IsRequired(); e.HasIndex(x => new { x.ClientId, x.Slug }).IsUnique();
            e.HasOne<ClientMaster>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasAlternateKey(x => new { x.ClientId, x.Id });
        });
        modelBuilder.Entity<Product>(e => {
            e.ToTable("Products"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(180).IsRequired(); e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Price).HasPrecision(18, 2); e.Property(x => x.ImageUrl).HasMaxLength(1000); e.Property(x => x.Badge).HasMaxLength(80);
            e.HasIndex(x => new { x.ClientId, x.Slug }).IsUnique();
            e.HasOne<ClientMaster>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Category>().WithMany().HasForeignKey(x => new { x.ClientId, x.CategoryId }).HasPrincipalKey(x => new { x.ClientId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });


        modelBuilder.Entity<CustomerOrder>(e =>
        {
            e.ToTable("CustomerOrders");
            e.HasKey(x => x.Id);

            e.Property(x => x.OrderNumber)
                .HasMaxLength(40)
                .IsRequired();

            e.Property(x => x.CustomerName)
                .HasMaxLength(120)
                .IsRequired();

            e.Property(x => x.CustomerPhone)
                .HasMaxLength(20)
                .IsRequired();

            e.Property(x => x.CakeName)
                .HasMaxLength(160)
                .IsRequired();

            e.Property(x => x.CakeSize)
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.Customization)
                .HasMaxLength(1000);

            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.CompletedUtc);

            e.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.CreatedUtc).IsRequired();
            e.Property(x => x.UpdatedUtc).IsRequired();

            e.HasIndex(x => new { x.ClientId, x.OrderNumber })
                .IsUnique();

            e.HasIndex(x => new { x.ClientId, x.Status, x.CreatedUtc });

            e.HasOne<CakeShop.Domain.Tenants.ClientMaster>()
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasAlternateKey(x => new { x.ClientId, x.Id });
        });

        modelBuilder.Entity<CustomerOrderItem>(e =>
        {
            e.ToTable("CustomerOrderItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProductName).HasMaxLength(160).IsRequired();
            e.Property(x => x.SizeLabel).HasMaxLength(30).IsRequired();
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.LineTotal).HasPrecision(18, 2);
            e.HasIndex(x => new { x.ClientId, x.OrderId });
            e.HasOne<CustomerOrder>().WithMany()
                .HasForeignKey(x => new { x.ClientId, x.OrderId })
                .HasPrincipalKey(x => new { x.ClientId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Product>().WithMany()
                .HasForeignKey(x => new { x.ClientId, x.ProductId })
                .HasPrincipalKey(x => new { x.ClientId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ProductVariant>().WithMany()
                .HasForeignKey(x => new { x.ClientId, x.ProductVariantId })
                .HasPrincipalKey(x => new { x.ClientId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderStatusHistory>(e =>
        {
            e.ToTable("OrderStatusHistories");

            e.HasKey(x => x.Id);

            e.Property(x => x.PreviousStatus)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.NewStatus)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.ChangedUtc)
                .IsRequired();

            e.Property(x => x.ChangedBy)
                .HasMaxLength(254);

            // Efficiently retrieve an order's history in chronological order.
            e.HasIndex(x => new
            {
                x.ClientId,
                x.OrderId,
                x.ChangedUtc
            });

            // Ensure the order belongs to the same tenant.
            e.HasOne<CustomerOrder>()
                .WithMany()
                .HasForeignKey(x => new { x.ClientId, x.OrderId })
                .HasPrincipalKey(x => new { x.ClientId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Expense>(e =>
        {
            e.ToTable("Expenses");
            e.HasKey(x => x.Id);

            e.Property(x => x.Category)
                .HasMaxLength(80)
                .IsRequired();

            e.Property(x => x.Amount)
                .HasPrecision(18, 2);

            e.Property(x => x.Description)
                .HasMaxLength(500);

            e.Property(x => x.ExpenseDate)
                .IsRequired();

            e.HasIndex(x => new { x.ClientId, x.ExpenseDate });

            e.HasOne<ClientMaster>()
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        modelBuilder.Entity<ProductVariant>(e =>
        {
            e.ToTable("ProductVariants");
            e.HasKey(x => x.Id);

            e.Property(x => x.SizeLabel)
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.Price)
                .HasPrecision(18, 2);

            e.HasIndex(x => new { x.ClientId, x.ProductId, x.WeightInGrams })
                .IsUnique();

            e.HasOne<Product>()
                .WithMany()
                .HasForeignKey(x => new { x.ClientId, x.ProductId })
                .HasPrincipalKey(x => new { x.ClientId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentTransaction>(e =>
        {
            e.ToTable("PaymentTransactions");
            e.HasKey(x => x.Id);

            e.Property(x => x.Provider)
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.Currency)
                .HasMaxLength(3)
                .IsRequired();

            e.Property(x => x.Amount)
                .HasPrecision(18, 2);

            e.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.ProviderOrderId)
                .HasMaxLength(100);

            e.Property(x => x.ProviderPaymentId)
                .HasMaxLength(100);

            e.HasIndex(x => new { x.ClientId, x.OrderId });

            e.HasIndex(x => new { x.Provider, x.ProviderOrderId })
                .IsUnique()
                .HasFilter("[ProviderOrderId] IS NOT NULL");

            e.HasIndex(x => new { x.Provider, x.ProviderPaymentId })
                .IsUnique()
                .HasFilter("[ProviderPaymentId] IS NOT NULL");

            e.HasOne<CakeShop.Domain.Tenants.ClientMaster>()
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<CustomerOrder>()
                .WithMany()
                .HasForeignKey(x => new { x.ClientId, x.OrderId })
                .HasPrincipalKey(x => new { x.ClientId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });


        modelBuilder.Entity<TenantPaymentSettings>(e =>
        {
            e.ToTable("TenantPaymentSettings");
            e.HasKey(x => x.Id);

            e.Property(x => x.Provider)
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.Environment)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsRequired();

            e.Property(x => x.MerchantAccountReference)
                .HasMaxLength(150);

            e.Property(x => x.KeyId)
                .HasMaxLength(150);

            e.Property(x => x.CredentialSecretReference)
                .HasMaxLength(500);

            e.Property(x => x.IsEnabled).IsRequired();
            e.Property(x => x.CreatedUtc).IsRequired();
            e.Property(x => x.UpdatedUtc).IsRequired();

            // One provider/environment configuration per tenant.
            e.HasIndex(x => new
            {
                x.ClientId,
                x.Provider,
                x.Environment
            }).IsUnique();

            e.HasOne<ClientMaster>()
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentTransaction>(e =>
        {
            e.ToTable("PaymentTransactions");
            e.HasKey(x => x.Id);

            e.Property(x => x.Provider)
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.Currency)
                .HasMaxLength(3)
                .IsRequired();

            e.Property(x => x.Amount)
                .HasPrecision(18, 2);

            e.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.ProviderOrderId).HasMaxLength(100);
            e.Property(x => x.ProviderPaymentId).HasMaxLength(100);

            e.HasIndex(x => new { x.ClientId, x.OrderId });

            e.HasIndex(x => new { x.Provider, x.ProviderOrderId })
                .IsUnique()
                .HasFilter("[ProviderOrderId] IS NOT NULL");

            e.HasIndex(x => new { x.Provider, x.ProviderPaymentId })
                .IsUnique()
                .HasFilter("[ProviderPaymentId] IS NOT NULL");

            e.HasOne<ClientMaster>()
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<CustomerOrder>()
                .WithMany()
                .HasForeignKey(x => new { x.ClientId, x.OrderId })
                .HasPrincipalKey(x => new { x.ClientId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProcessedPaymentWebhook>(e =>
        {
            e.ToTable("ProcessedPaymentWebhooks");
            e.HasKey(x => x.Id);

            e.Property(x => x.Provider)
                .HasMaxLength(40)
                .IsRequired();

            e.Property(x => x.EventId)
                .HasMaxLength(200)
                .IsRequired();

            e.Property(x => x.ProcessedUtc)
                .IsRequired();

            e.HasIndex(x => new { x.Provider, x.EventId })
                .IsUnique();
        });

    }
}
