using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Data.Seed;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Tests;

public class DbSeederPaversTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "nsi-seed-" + Guid.NewGuid().ToString("N"));
    private readonly string _db = Guid.NewGuid().ToString();

    private AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_db).Options);

    [Fact]
    public async Task Pavers_are_added_once_with_photo_texture_and_visualizer()
    {
        await using (var db = Context())
            await DbSeeder.SeedAsync(db, _webRoot);
        await using (var db = Context())
            await DbSeeder.SeedAsync(db, _webRoot); // a restart must not duplicate them

        await using var check = Context();
        var pavers = await check.Products.Include(p => p.Category)
            .Where(p => p.Category.Name == "Павета").ToListAsync();

        Assert.Equal(9, pavers.Count);
        Assert.Equal(9, pavers.Select(p => p.Name).Distinct().Count());
        Assert.All(pavers, p =>
        {
            Assert.Equal(UnitType.Sqm, p.Unit);
            Assert.True(p.PriceWithVat > 0);
            Assert.Equal(p.PriceWithVat, p.PriceWithoutVat + p.VatAmount);
            Assert.True(p.IsVisualizerEnabled);
            Assert.Equal($"/uploads/textures/{p.Id}_texture.jpg", p.TextureImagePath);
            Assert.True(File.Exists(Path.Combine(_webRoot, "uploads", "textures", $"{p.Id}_texture.jpg")));
            Assert.NotNull(p.ImagePath);
            Assert.True(File.Exists(Path.Combine(_webRoot, p.ImagePath!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))));
            Assert.True(p.TextureWidthMeters > 0);
        });
    }

    [Fact]
    public async Task Pavers_reach_a_database_that_already_has_a_catalog()
    {
        await using (var db = Context())
        {
            var category = new Category { Name = "Гнайс" };
            db.Products.Add(new Product { Name = "Съществуващ", Category = category, PriceWithVat = 1m, PriceWithoutVat = 1m });
            await db.SaveChangesAsync();
            await DbSeeder.SeedAsync(db, _webRoot);
        }

        await using var check = Context();
        Assert.Equal(9, await check.Products.CountAsync(p => p.Category.Name == "Павета"));
        Assert.Equal(10, await check.Products.CountAsync()); // the full seed catalog is skipped: products exist
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
            Directory.Delete(_webRoot, recursive: true);
    }
}
