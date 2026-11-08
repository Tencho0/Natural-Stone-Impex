using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class StorefrontQueryServiceTests
{
    private static AppDbContext CreateContext()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Categories.AddRange(
            new Category { Id = 1, Name = "A" }, new Category { Id = 2, Name = "B" },
            new Category { Id = 3, Name = "C" }, new Category { Id = 4, Name = "D" });
        return db;
    }

    private static Product P(int id, string name, int cat, decimal price = 10m, string? image = "/img.jpg", bool active = true) =>
        new() { Id = id, Name = name, CategoryId = cat, PriceWithoutVat = price, PriceWithVat = price,
            Unit = UnitType.Kg, ImagePath = image, IsActive = active };

    [Fact]
    public async Task Featured_round_robins_categories_and_skips_unpriced_or_imageless()
    {
        await using var db = CreateContext();
        db.Products.AddRange(P(1, "a1", 1), P(2, "a2", 1), P(3, "a3", 1), P(4, "b1", 2),
            P(5, "c1", 3, price: 0m), P(6, "d1", 4, image: null), P(7, "d2", 4, image: "  "));
        await db.SaveChangesAsync();

        var result = await new StorefrontQueryService(db).GetFeaturedAsync(4);

        Assert.Equal(new[] { "a1", "b1", "a2", "a3" }, result.Select(p => p.Name));
        Assert.Equal("кг", result[0].UnitDisplay);
        Assert.Equal("A", result[0].CategoryName);
        Assert.Empty(await new StorefrontQueryService(db).GetFeaturedAsync(0));
    }

    [Fact]
    public async Task Active_product_lookup_hides_inactive()
    {
        await using var db = CreateContext();
        db.Products.AddRange(P(1, "a1", 1), P(2, "a2", 1, active: false));
        await db.SaveChangesAsync();
        var service = new StorefrontQueryService(db);

        Assert.NotNull(await service.GetActiveProductAsync(1));
        Assert.Null(await service.GetActiveProductAsync(2));
        Assert.Null(await service.GetActiveProductAsync(99));
    }

    [Fact]
    public async Task GetByIds_returns_inactive_with_flag()
    {
        await using var db = CreateContext();
        db.Products.AddRange(P(1, "a1", 1), P(2, "a2", 1, active: false));
        await db.SaveChangesAsync();
        var service = new StorefrontQueryService(db);

        var result = await service.GetByIdsAsync(new[] { 2, 99, 1 });

        Assert.Equal(new[] { 2, 1 }, result.Select(p => p.Id));
        Assert.False(result[0].IsActive);
        Assert.True(result[1].IsActive);
        Assert.Empty(await service.GetByIdsAsync(Array.Empty<int>()));
    }

    [Fact]
    public async Task Category_photos_pick_first_by_name_with_image()
    {
        await using var db = CreateContext();
        db.Products.AddRange(P(1, "a1", 1, image: null), P(2, "a2", 1, image: "/a2.jpg"), P(3, "a3", 1, image: "/a3.jpg"),
            P(4, "b1", 2, image: "/b1.jpg", active: false), P(5, "c1", 3, image: " "));
        await db.SaveChangesAsync();

        var photos = await new StorefrontQueryService(db).GetCategoryPhotosAsync();

        Assert.Single(photos);
        Assert.Equal("/a2.jpg", photos[1]);
    }

    [Fact]
    public async Task Sitemap_lists_only_active_products()
    {
        await using var db = CreateContext();
        var t = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var p1 = P(1, "a1", 1); p1.UpdatedAt = t;
        db.Products.AddRange(p1, P(2, "a2", 1, active: false));
        await db.SaveChangesAsync();

        var result = await new StorefrontQueryService(db).GetSitemapProductsAsync();

        Assert.Equal((1, t), Assert.Single(result));
    }
}
