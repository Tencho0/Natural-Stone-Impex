using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Tests;

public class VisualizerProductsTests
{
    private static AppDbContext CreateContext()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Categories.Add(new Category { Id = 1, Name = "Павета" });
        db.Products.AddRange(
            new Product { Id = 1, Name = "Включен с текстура", CategoryId = 1, Unit = UnitType.Sqm,
                IsActive = true, IsVisualizerEnabled = true,
                TextureImagePath = "/uploads/textures/1_texture.jpg", TextureWidthMeters = 1.2m,
                PriceWithoutVat = 20m, VatAmount = 4m, PriceWithVat = 24m },
            new Product { Id = 2, Name = "Изключен", CategoryId = 1, Unit = UnitType.Sqm,
                IsActive = true, IsVisualizerEnabled = false,
                TextureImagePath = "/uploads/textures/2_texture.jpg" },
            new Product { Id = 3, Name = "Без текстура", CategoryId = 1, Unit = UnitType.Sqm,
                IsActive = true, IsVisualizerEnabled = true, TextureImagePath = null },
            new Product { Id = 4, Name = "Неактивен", CategoryId = 1, Unit = UnitType.Sqm,
                IsActive = false, IsVisualizerEnabled = true,
                TextureImagePath = "/uploads/textures/4_texture.jpg" });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Returns_only_active_enabled_products_with_textures()
    {
        await using var db = CreateContext();
        var service = new ProductService(db, new FakeWebHostEnvironment());

        var result = await service.GetVisualizerProductsAsync();

        var product = Assert.Single(result);
        Assert.Equal(1, product.Id);
        Assert.Equal("/uploads/textures/1_texture.jpg", product.TexturePath);
        Assert.Equal(1.2m, product.TextureWidthMeters);
        Assert.Equal("м²", product.UnitDisplay);
        Assert.Equal(24m, product.PriceWithVat);
    }
}
