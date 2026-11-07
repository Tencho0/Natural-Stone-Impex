using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Data.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, string webRootPath)
    {
        await SeedAdminUserAsync(context);
        await SeedCategoriesAsync(context);
        await SeedProductsAsync(context, webRootPath);
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

    private sealed record SeedProduct(
        string Name, string Category, string? Code, string Unit, string? UnitNote,
        decimal PriceWithoutVat, decimal VatAmount, decimal PriceWithVat, string? Image);

    private static List<SeedProduct> LoadSeedProducts()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "SeedData", "products.json");
        var json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<List<SeedProduct>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new List<SeedProduct>();
    }

    private static async Task SeedCategoriesAsync(AppDbContext context)
    {
        if (await context.Categories.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        var names = LoadSeedProducts().Select(p => p.Category).Distinct();
        context.Categories.AddRange(names.Select(n => new Category { Name = n, CreatedAt = now, UpdatedAt = now }));
        await context.SaveChangesAsync();
    }

    // Seeds the product catalog taken from naturalstoneimpex.com (prices converted from BGN to EUR at 1.95583,
    // assumed VAT-inclusive at 20%). Products without a published price are seeded with 0.00 and stock 0.
    private static async Task SeedProductsAsync(AppDbContext context, string webRootPath)
    {
        if (await context.Products.AnyAsync())
            return;

        var categories = await context.Categories.ToDictionaryAsync(c => c.Name, c => c.Id);
        var seedDir = Path.Combine(AppContext.BaseDirectory, "SeedData", "products");
        var uploadsDir = Path.Combine(webRootPath, "uploads", "products");
        Directory.CreateDirectory(uploadsDir);
        var now = DateTime.UtcNow;

        var products = new List<Product>();
        foreach (var p in LoadSeedProducts())
        {
            string? imagePath = null;
            if (p.Image is not null)
            {
                var src = Path.Combine(seedDir, p.Image);
                if (File.Exists(src))
                {
                    File.Copy(src, Path.Combine(uploadsDir, p.Image), overwrite: true);
                    imagePath = $"/uploads/products/{p.Image}";
                }
            }

            // UnitType supports only кг and м²; the one м³ product is seeded without a price and keeps its price in the description.
            var perCubic = p.UnitNote == "m3";
            var description = p.Code is null ? null : $"Код: {p.Code}";
            if (perCubic)
                description += $"; цена: {p.PriceWithVat:0.00} € / м³";

            products.Add(new Product
            {
                Name = p.Name,
                Description = description,
                CategoryId = categories[p.Category],
                PriceWithoutVat = perCubic ? 0m : p.PriceWithoutVat,
                VatAmount = perCubic ? 0m : p.VatAmount,
                PriceWithVat = perCubic ? 0m : p.PriceWithVat,
                Unit = p.Unit == "Kg" ? UnitType.Kg : UnitType.Sqm,
                StockQuantity = 0m,
                ImagePath = imagePath,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        context.Products.AddRange(products);
        await context.SaveChangesAsync();
    }
}
