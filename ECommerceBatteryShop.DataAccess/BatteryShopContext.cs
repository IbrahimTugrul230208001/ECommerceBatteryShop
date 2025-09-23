using ECommerceBatteryShop.Domain.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBatteryShop.DataAccess;

public sealed class BatteryShopContext : DbContext, IDataProtectionKeyContext
{
    public BatteryShopContext(DbContextOptions<BatteryShopContext> options) : base(options) { }

    // REQUIRED for Data Protection persistence
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        const string trCollation = "tr_icu_det";
        modelBuilder.Entity<Product>(e => e.Property(p => p.Name).UseCollation(trCollation));

        // ⚠ You’re on PostgreSQL. Replace SQL Server brackets in filters.
        modelBuilder.Entity<Cart>(b =>
        {
            b.HasIndex(c => c.UserId).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
            b.HasIndex(c => c.AnonId).IsUnique().HasFilter("\"AnonId\" IS NOT NULL");
        });

        modelBuilder.Entity<Cart>()
            .ToTable(tb => tb.HasCheckConstraint(
                "CK_Cart_Owner",
                "(\"UserId\" IS NOT NULL AND \"AnonId\" IS NULL) OR (\"UserId\" IS NULL AND \"AnonId\" IS NOT NULL)"
            ));

        base.OnModelCreating(modelBuilder);
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatus> OrderStatuses => Set<OrderStatus>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<User> Users => Set<User>();
    public DbSet<FavoriteList> FavoriteLists => Set<FavoriteList>();
    public DbSet<FavoriteListItem> FavoriteListItems => Set<FavoriteListItem>();
}
