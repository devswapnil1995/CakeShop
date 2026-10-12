using CakeShop.Domain.Catalog;
using CakeShop.Domain.Tenants;
using CakeShop.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace CakeShop.Infrastructure.Persistence;

public sealed class CakeShopDbContext(DbContextOptions<CakeShopDbContext> options) : DbContext(options)
{
    public DbSet<ClientMaster> Clients => Set<ClientMaster>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
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
    }
}
