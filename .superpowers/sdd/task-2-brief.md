### Task 2: Visualizer products endpoint + texture upload + static-file CORS

**Files:**
- Create: `src/NaturalStoneImpex.Api/Models/DTOs/VisualizerProductDto.cs`
- Modify: `src/NaturalStoneImpex.Api/Services/IProductService.cs`
- Modify: `src/NaturalStoneImpex.Api/Services/ProductService.cs`
- Create: `src/NaturalStoneImpex.Api/Controllers/VisualizerController.cs`
- Modify: `src/NaturalStoneImpex.Api/Controllers/ProductsController.cs`
- Modify: `src/NaturalStoneImpex.Api/Program.cs` (static files, ~line 86)
- Test: `tests/NaturalStoneImpex.Api.Tests/VisualizerProductsTests.cs`

**Interfaces:**
- Consumes: `Product` visualizer fields from Task 1.
- Produces: `VisualizerProductDto` record (`Id`, `Name`, `ImagePath`, `TexturePath`, `TextureWidthMeters`, `PriceWithoutVat`, `VatAmount`, `PriceWithVat`, `Unit`, `UnitDisplay`, `CategoryId`, `CategoryName`); `IProductService.GetVisualizerProductsAsync()` → `Task<List<VisualizerProductDto>>`; `IProductService.UploadTextureAsync(int id, IFormFile file)` → `Task<(string? TexturePath, string? Error)>`; HTTP `GET /api/visualizer/products` (public) and `POST /api/products/{id}/texture` (admin). Tasks 6, 9, 13 rely on these exact names.

- [ ] **Step 1: Write the failing tests**

Create `tests/NaturalStoneImpex.Api.Tests/VisualizerProductsTests.cs`:

```csharp
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
```

Add the fake environment helper `tests/NaturalStoneImpex.Api.Tests/FakeWebHostEnvironment.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace NaturalStoneImpex.Api.Tests;

public class FakeWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "nsi-tests-wwwroot");
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ApplicationName { get; set; } = "NaturalStoneImpex.Api";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public string EnvironmentName { get; set; } = "Development";
}
```

(Delete the `using Moq;` line from the test — it is only there to remind you not to add Moq; fakes are enough.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter VisualizerProductsTests`
Expected: FAIL — `'IProductService' does not contain a definition for 'GetVisualizerProductsAsync'`.

- [ ] **Step 3: Create the DTO**

Create `src/NaturalStoneImpex.Api/Models/DTOs/VisualizerProductDto.cs`:

```csharp
namespace NaturalStoneImpex.Api.Models.DTOs;

public record VisualizerProductDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ImagePath { get; init; }
    public string TexturePath { get; init; } = string.Empty;
    public decimal TextureWidthMeters { get; init; }
    public decimal PriceWithoutVat { get; init; }
    public decimal VatAmount { get; init; }
    public decimal PriceWithVat { get; init; }
    public int Unit { get; init; }
    public string UnitDisplay { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
}
```

- [ ] **Step 4: Implement service methods**

In `src/NaturalStoneImpex.Api/Services/IProductService.cs` add to the interface:

```csharp
    Task<List<VisualizerProductDto>> GetVisualizerProductsAsync();
    Task<(string? TexturePath, string? Error)> UploadTextureAsync(int id, IFormFile file);
```

In `src/NaturalStoneImpex.Api/Services/ProductService.cs` add:

```csharp
    public async Task<List<VisualizerProductDto>> GetVisualizerProductsAsync()
    {
        return await _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive && p.IsVisualizerEnabled && p.TextureImagePath != null)
            .OrderBy(p => p.Name)
            .Select(p => new VisualizerProductDto
            {
                Id = p.Id,
                Name = p.Name,
                ImagePath = p.ImagePath,
                TexturePath = p.TextureImagePath!,
                TextureWidthMeters = p.TextureWidthMeters,
                PriceWithoutVat = p.PriceWithoutVat,
                VatAmount = p.VatAmount,
                PriceWithVat = p.PriceWithVat,
                Unit = (int)p.Unit,
                UnitDisplay = p.Unit == UnitType.Kg ? "кг" : "м²",
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name
            })
            .ToListAsync();
    }

    public async Task<(string? TexturePath, string? Error)> UploadTextureAsync(int id, IFormFile file)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is null)
            return (null, "Продуктът не е намерен.");

        var allowedTypes = new[] { "image/jpeg", "image/png" };
        if (!allowedTypes.Contains(file.ContentType) || file.Length > 5 * 1024 * 1024)
            return (null, "Позволени са само JPG и PNG файлове до 5MB.");

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "textures");
        Directory.CreateDirectory(uploadsDir);

        if (!string.IsNullOrEmpty(product.TextureImagePath))
        {
            var oldPath = Path.Combine(_env.WebRootPath, product.TextureImagePath.TrimStart('/'));
            if (File.Exists(oldPath))
                File.Delete(oldPath);
        }

        var extension = file.ContentType == "image/png" ? ".png" : ".jpg";
        var fileName = $"{id}_texture{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var texturePath = $"/uploads/textures/{fileName}";
        product.TextureImagePath = texturePath;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (texturePath, null);
    }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter VisualizerProductsTests`
Expected: PASS.

- [ ] **Step 6: Add the controller endpoints**

Create `src/NaturalStoneImpex.Api/Controllers/VisualizerController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Controllers;

[ApiController]
[Route("api/visualizer")]
public class VisualizerController : ControllerBase
{
    private readonly IProductService _productService;

    public VisualizerController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _productService.GetVisualizerProductsAsync();
        return Ok(products);
    }
}
```

In `src/NaturalStoneImpex.Api/Controllers/ProductsController.cs`, after the `UploadImage` action add:

```csharp
    [Authorize]
    [HttpPost("{id}/texture")]
    public async Task<IActionResult> UploadTexture(int id, IFormFile texture)
    {
        if (texture is null || texture.Length == 0)
            return BadRequest(new { error = "Файлът е задължителен." });

        var (texturePath, error) = await _productService.UploadTextureAsync(id, texture);

        if (error == "Продуктът не е намерен.")
            return NotFound(new { error });

        if (error is not null)
            return BadRequest(new { error });

        return Ok(new { texturePath });
    }
```

- [ ] **Step 7: Allow cross-origin use of uploaded images in WebGL**

The client (`:5002`) loads texture images from the API origin (`:5001`) into WebGL; without CORS headers on static files the canvas becomes tainted and `toDataURL`/`readPixels` throw. In `src/NaturalStoneImpex.Api/Program.cs` replace `app.UseStaticFiles();` with:

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    // Product/texture images are public; allow the Blazor client (different port)
    // to load them into WebGL without tainting the canvas.
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*")
});
```

- [ ] **Step 8: Build and verify manually**

```powershell
dotnet build
```
Expected: success. Then run the API (`dotnet run --project src/NaturalStoneImpex.Api`) and check `curl -k https://localhost:5001/api/visualizer/products` returns `[]` (no products enabled yet) with HTTP 200.

- [ ] **Step 9: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): products endpoint and texture upload"
```

---

