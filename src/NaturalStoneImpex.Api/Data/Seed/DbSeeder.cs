using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Data.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        await SeedAdminUserAsync(context);
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
}
