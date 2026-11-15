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
        await SeedPaversAsync(context, webRootPath);
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

    // Seeds the product catalog taken from naturalstoneimpex.com (prices in EUR, VAT-inclusive at 20%). Products without a published price are seeded with 0.00 and stock 0.
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

    private sealed record SeedPaver(
        string Name, string Category, string Description, string Unit,
        decimal PriceWithoutVat, decimal VatAmount, decimal PriceWithVat,
        string Image, string Texture, decimal TextureWidthMeters);

    // Concrete pavers (sofurban.com) with visualizer textures drawn from their real sizes. Unlike the catalog above,
    // they are added to existing databases too: each one only when no product has its name yet, so restarts never
    // duplicate them. Prices are placeholders for the admin to correct.
    private static async Task SeedPaversAsync(AppDbContext context, string webRootPath)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "SeedData", "pavers.json");
        if (!File.Exists(file))
            return;
        var pavers = JsonSerializer.Deserialize<List<SeedPaver>>(File.ReadAllText(file),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var names = pavers.Select(p => p.Name).ToList();
        var existing = await context.Products.Where(p => names.Contains(p.Name)).Select(p => p.Name).ToListAsync();
        var missing = pavers.Where(p => !existing.Contains(p.Name)).ToList();
        if (missing.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var categories = await context.Categories.ToDictionaryAsync(c => c.Name, c => c);
        foreach (var name in missing.Select(p => p.Category).Distinct().Where(n => !categories.ContainsKey(n)))
        {
            var category = new Category { Name = name, CreatedAt = now, UpdatedAt = now };
            context.Categories.Add(category);
            categories[name] = category;
        }

        var seedDir = Path.Combine(AppContext.BaseDirectory, "SeedData");
        var imagesDir = Path.Combine(webRootPath, "uploads", "products");
        var texturesDir = Path.Combine(webRootPath, "uploads", "textures");
        Directory.CreateDirectory(imagesDir);
        Directory.CreateDirectory(texturesDir);

        var added = new List<(Product Product, SeedPaver Seed)>();
        foreach (var p in missing)
        {
            var product = new Product
            {
                Name = p.Name,
                Description = p.Description,
                Category = categories[p.Category],
                PriceWithoutVat = p.PriceWithoutVat,
                VatAmount = p.VatAmount,
                PriceWithVat = p.PriceWithVat,
                Unit = p.Unit == "Kg" ? UnitType.Kg : UnitType.Sqm,
                StockQuantity = 0m,
                ImagePath = CopySeedFile(Path.Combine(seedDir, "products", p.Image), imagesDir, p.Image, "products"),
                TextureWidthMeters = p.TextureWidthMeters,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.Products.Add(product);
            added.Add((product, p));
        }
        await context.SaveChangesAsync();

        // Textures are named like admin uploads ("{id}_texture.jpg"), so they need the new ids.
        foreach (var (product, seed) in added)
        {
            var name = $"{product.Id}_texture{Path.GetExtension(seed.Texture)}";
            product.TextureImagePath = CopySeedFile(Path.Combine(seedDir, "textures", seed.Texture), texturesDir, name, "textures");
            product.IsVisualizerEnabled = product.TextureImagePath is not null;
        }
        await context.SaveChangesAsync();
    }

    /// <summary>Copies a seed file into wwwroot/uploads/{folder} and returns its web path, or null when it is missing.</summary>
    private static string? CopySeedFile(string source, string targetDir, string targetName, string folder)
    {
        if (!File.Exists(source))
            return null;
        File.Copy(source, Path.Combine(targetDir, targetName), overwrite: true);
        return $"/uploads/{folder}/{targetName}";
    }
}
