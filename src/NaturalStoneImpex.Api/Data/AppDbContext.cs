using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderCustomerInfo> OrderCustomerInfos => Set<OrderCustomerInfo>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.PriceWithoutVat).HasPrecision(18, 2);
            entity.Property(e => e.VatAmount).HasPrecision(18, 2);
            entity.Property(e => e.PriceWithVat).HasPrecision(18, 2);
            entity.Property(e => e.StockQuantity).HasPrecision(18, 2);
            entity.Property(e => e.ImagePath).HasMaxLength(500);

            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.Name, e.CategoryId }).IsUnique();
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.Property(e => e.OrderNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.DeliveryFee).HasPrecision(18, 2);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<OrderCustomerInfo>(entity =>
        {
            entity.HasOne(e => e.Order)
                  .WithOne(o => o.CustomerInfo)
                  .HasForeignKey<OrderCustomerInfo>(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.OrderId).IsUnique();
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.Eik).HasMaxLength(13);
            entity.Property(e => e.Mol).HasMaxLength(200);
            entity.Property(e => e.ContactPerson).HasMaxLength(200);
            entity.Property(e => e.ContactPhone).HasMaxLength(20);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 2);
            entity.Property(e => e.UnitPriceWithoutVat).HasPrecision(18, 2);
            entity.Property(e => e.VatAmount).HasPrecision(18, 2);
            entity.Property(e => e.UnitPriceWithVat).HasPrecision(18, 2);

            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.Property(e => e.SupplierName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();

            entity.HasIndex(e => e.EntryDate);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.Property(e => e.Quantity).HasPrecision(18, 2);
            entity.Property(e => e.PurchasePrice).HasPrecision(18, 2);

            entity.HasOne(e => e.Invoice)
                  .WithMany(i => i.Items)
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
