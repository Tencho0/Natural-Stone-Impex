using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Tests;

public class ProductVisualizerFieldsTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Product_persists_visualizer_fields()
    {
        await using var db = CreateContext();
        db.Categories.Add(new Category { Id = 1, Name = "Павета" });
        db.Products.Add(new Product
        {
            Id = 1,
            Name = "Гнайс сив",
            CategoryId = 1,
            Unit = UnitType.Sqm,
            IsVisualizerEnabled = true,
            TextureImagePath = "/uploads/textures/1_texture.jpg",
            TextureWidthMeters = 1.20m
        });
        await db.SaveChangesAsync();

        var saved = await db.Products.SingleAsync(p => p.Id == 1);
        Assert.True(saved.IsVisualizerEnabled);
        Assert.Equal("/uploads/textures/1_texture.jpg", saved.TextureImagePath);
        Assert.Equal(1.20m, saved.TextureWidthMeters);
    }

    [Fact]
    public void New_product_defaults_visualizer_off_with_one_meter_texture()
    {
        var product = new Product();
        Assert.False(product.IsVisualizerEnabled);
        Assert.Null(product.TextureImagePath);
        Assert.Equal(1.00m, product.TextureWidthMeters);
    }
}
