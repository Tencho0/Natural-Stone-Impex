using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Data.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        await SeedAdminUserAsync(context);
        await SeedCategoriesAsync(context);
        await SeedProductsAsync(context);
    }

    private static async Task SeedAdminUserAsync(AppDbContext context)
    {
        if (await context.AdminUsers.AnyAsync())
            return;

        var hasher = new PasswordHasher<AdminUser>();
        var admin = new AdminUser
        {
            Username = "admin",
            CreatedAt = DateTime.UtcNow
        };
        admin.PasswordHash = hasher.HashPassword(admin, "Admin123!");

        context.AdminUsers.Add(admin);
        await context.SaveChangesAsync();
    }

    private static async Task SeedCategoriesAsync(AppDbContext context)
    {
        if (await context.Categories.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        var categories = new[]
        {
            new Category { Name = "Натурален камък", CreatedAt = now, UpdatedAt = now },
            new Category { Name = "Цимент", CreatedAt = now, UpdatedAt = now },
            new Category { Name = "Пясък и чакъл", CreatedAt = now, UpdatedAt = now },
            new Category { Name = "Плочки", CreatedAt = now, UpdatedAt = now },
            new Category { Name = "Инструменти", CreatedAt = now, UpdatedAt = now }
        };

        context.Categories.AddRange(categories);
        await context.SaveChangesAsync();
    }

    private static async Task SeedProductsAsync(AppDbContext context)
    {
        if (await context.Products.AnyAsync())
            return;

        var categories = await context.Categories.ToDictionaryAsync(c => c.Name, c => c.Id);
        var now = DateTime.UtcNow;

        var products = new[]
        {
            new Product
            {
                Name = "Гранит сив",
                CategoryId = categories["Натурален камък"],
                PriceWithoutVat = 25.00m,
                VatAmount = 5.00m,
                PriceWithVat = 30.00m,
                Unit = UnitType.Sqm,
                StockQuantity = 150.00m,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Product
            {
                Name = "Мрамор бял",
                CategoryId = categories["Натурален камък"],
                PriceWithoutVat = 40.00m,
                VatAmount = 8.00m,
                PriceWithVat = 48.00m,
                Unit = UnitType.Sqm,
                StockQuantity = 80.00m,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Product
            {
                Name = "Цимент 25кг",
                CategoryId = categories["Цимент"],
                PriceWithoutVat = 5.00m,
                VatAmount = 1.00m,
                PriceWithVat = 6.00m,
                Unit = UnitType.Kg,
                StockQuantity = 500.00m,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Product
            {
                Name = "Пясък фин",
                CategoryId = categories["Пясък и чакъл"],
                PriceWithoutVat = 0.08m,
                VatAmount = 0.02m,
                PriceWithVat = 0.10m,
                Unit = UnitType.Kg,
                StockQuantity = 2000.00m,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Product
            {
                Name = "Гранитогрес 60x60",
                CategoryId = categories["Плочки"],
                PriceWithoutVat = 15.00m,
                VatAmount = 3.00m,
                PriceWithVat = 18.00m,
                Unit = UnitType.Sqm,
                StockQuantity = 200.00m,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        context.Products.AddRange(products);
        await context.SaveChangesAsync();
    }
}
