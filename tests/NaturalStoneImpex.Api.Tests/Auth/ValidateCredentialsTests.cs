using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Tests.Auth;

public class ValidateCredentialsTests
{
    private static async Task<AuthService> CreateServiceAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var hasher = new PasswordHasher<AdminUser>();
        var admin = new AdminUser { Username = "admin", CreatedAt = DateTime.UtcNow };
        admin.PasswordHash = hasher.HashPassword(admin, "Admin123!");
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();

        return new AuthService(db, hasher, new ConfigurationBuilder().Build());
    }

    [Fact]
    public async Task Returns_user_for_correct_password()
    {
        var service = await CreateServiceAsync();

        var user = await service.ValidateCredentialsAsync("admin", "Admin123!");

        Assert.NotNull(user);
        Assert.Equal("admin", user.Username);
    }

    [Fact]
    public async Task Returns_null_for_wrong_password_or_unknown_user()
    {
        var service = await CreateServiceAsync();

        Assert.Null(await service.ValidateCredentialsAsync("admin", "wrong"));
        Assert.Null(await service.ValidateCredentialsAsync("nobody", "Admin123!"));
    }
}
