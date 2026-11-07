using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Tests;

// Uploading a new texture/image deletes the product's previous file. These tests pin down
// that the old file is only deleted when nothing else on the product still references it,
// and only when it lives inside the upload folder that the upload owns.
public class ProductUploadFileSafetyTests : IDisposable
{
    private readonly string _root;
    private readonly string _webRoot;

    public ProductUploadFileSafetyTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "nsi-upload-tests-" + Guid.NewGuid().ToString("N"));
        _webRoot = Path.Combine(_root, "wwwroot");
        Directory.CreateDirectory(Path.Combine(_webRoot, "uploads", "products"));
        Directory.CreateDirectory(Path.Combine(_webRoot, "uploads", "textures"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static AppDbContext CreateContext(Product product)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Categories.Add(new Category { Id = 1, Name = "Павета" });
        product.CategoryId = 1;
        product.Name = "Гнайс сив";
        product.Unit = UnitType.Sqm;
        product.IsActive = true;
        db.Products.Add(product);
        db.SaveChanges();
        return db;
    }

    private ProductService CreateService(AppDbContext db) =>
        new(db, new FakeWebHostEnvironment { WebRootPath = _webRoot });

    private string WriteWebFile(string webPath)
    {
        var fullPath = Path.Combine(_webRoot, webPath.TrimStart('/'));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, new byte[] { 1, 2, 3 });
        return fullPath;
    }

    private static IFormFile Jpeg(string fileName = "new.jpg") =>
        new FormFile(new MemoryStream(new byte[] { 9, 9, 9 }), 0, 3, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

    [Fact]
    public async Task Texture_upload_keeps_old_texture_file_shared_with_catalog_image()
    {
        const string shared = "/uploads/products/product-54.jpg";
        var sharedFile = WriteWebFile(shared);
        await using var db = CreateContext(new Product { Id = 84, ImagePath = shared, TextureImagePath = shared });

        var (texturePath, error) = await CreateService(db).UploadTextureAsync(84, Jpeg());

        Assert.Null(error);
        Assert.Equal("/uploads/textures/84_texture.jpg", texturePath);
        Assert.True(File.Exists(sharedFile), "Catalog image shared with the old texture must not be deleted.");
        var saved = await db.Products.SingleAsync(p => p.Id == 84);
        Assert.Equal(shared, saved.ImagePath);
        Assert.Equal("/uploads/textures/84_texture.jpg", saved.TextureImagePath);
    }

    [Fact]
    public async Task Texture_upload_deletes_previous_dedicated_texture()
    {
        const string oldTexture = "/uploads/textures/84_texture.png";
        var oldFile = WriteWebFile(oldTexture);
        await using var db = CreateContext(new Product
        {
            Id = 84, ImagePath = "/uploads/products/product-54.jpg", TextureImagePath = oldTexture
        });

        var (texturePath, error) = await CreateService(db).UploadTextureAsync(84, Jpeg());

        Assert.Null(error);
        Assert.False(File.Exists(oldFile), "Previous dedicated texture should be deleted.");
        Assert.True(File.Exists(Path.Combine(_webRoot, "uploads", "textures", "84_texture.jpg")));
        Assert.Equal("/uploads/textures/84_texture.jpg", texturePath);
    }

    [Theory]
    [InlineData("/../outside.txt")]
    [InlineData("/uploads/textures/../../../outside.txt")]
    public async Task Texture_upload_never_deletes_files_outside_textures_folder(string maliciousPath)
    {
        var outsideFile = Path.Combine(_root, "outside.txt");
        File.WriteAllBytes(outsideFile, new byte[] { 1 });
        await using var db = CreateContext(new Product { Id = 84, TextureImagePath = maliciousPath });

        var (_, error) = await CreateService(db).UploadTextureAsync(84, Jpeg());

        Assert.Null(error);
        Assert.True(File.Exists(outsideFile), "A path escaping the textures folder must not be deleted.");
    }

    [Fact]
    public async Task Texture_upload_never_deletes_files_in_other_upload_folders()
    {
        // Not shared with ImagePath, but it belongs to the products folder, not textures.
        const string productImage = "/uploads/products/product-99.jpg";
        var productFile = WriteWebFile(productImage);
        await using var db = CreateContext(new Product { Id = 84, TextureImagePath = productImage });

        var (_, error) = await CreateService(db).UploadTextureAsync(84, Jpeg());

        Assert.Null(error);
        Assert.True(File.Exists(productFile), "Texture upload must only delete files inside uploads/textures.");
    }

    [Fact]
    public async Task Image_upload_keeps_old_image_file_shared_with_texture()
    {
        const string shared = "/uploads/products/product-54.jpg";
        var sharedFile = WriteWebFile(shared);
        await using var db = CreateContext(new Product { Id = 84, ImagePath = shared, TextureImagePath = shared });

        var (imagePath, error) = await CreateService(db).UploadImageAsync(84, Jpeg("new.jpg"));

        Assert.Null(error);
        Assert.Equal("/uploads/products/84_new.jpg", imagePath);
        Assert.True(File.Exists(sharedFile), "Texture shared with the old catalog image must not be deleted.");
        var saved = await db.Products.SingleAsync(p => p.Id == 84);
        Assert.Equal(shared, saved.TextureImagePath);
    }

    [Fact]
    public async Task Image_upload_deletes_previous_dedicated_image()
    {
        const string oldImage = "/uploads/products/product-54.jpg";
        var oldFile = WriteWebFile(oldImage);
        await using var db = CreateContext(new Product
        {
            Id = 84, ImagePath = oldImage, TextureImagePath = "/uploads/textures/84_texture.jpg"
        });

        var (imagePath, error) = await CreateService(db).UploadImageAsync(84, Jpeg("new.jpg"));

        Assert.Null(error);
        Assert.False(File.Exists(oldFile), "Previous dedicated catalog image should be deleted.");
        Assert.True(File.Exists(Path.Combine(_webRoot, "uploads", "products", "84_new.jpg")));
        Assert.Equal("/uploads/products/84_new.jpg", imagePath);
    }

    [Theory]
    [InlineData("/../outside.txt")]
    [InlineData("/uploads/products/../../../outside.txt")]
    public async Task Image_upload_never_deletes_files_outside_products_folder(string maliciousPath)
    {
        var outsideFile = Path.Combine(_root, "outside.txt");
        File.WriteAllBytes(outsideFile, new byte[] { 1 });
        await using var db = CreateContext(new Product { Id = 84, ImagePath = maliciousPath });

        var (_, error) = await CreateService(db).UploadImageAsync(84, Jpeg("new.jpg"));

        Assert.Null(error);
        Assert.True(File.Exists(outsideFile), "A path escaping the products folder must not be deleted.");
    }
}
