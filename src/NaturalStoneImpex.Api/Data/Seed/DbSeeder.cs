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
}
