# Product Visualizer (Визуализатор) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Customers upload a photo of their terrain, tap the surface to pave, and see it re-textured with the exact texture of a selected paving product — with instant product switching.

**Architecture:** AI is used only for segmentation: a MobileSAM ONNX model runs on the API server's CPU (`Microsoft.ML.OnnxRuntime`) and turns customer taps into a surface mask. Everything visual happens in the browser: a plain-JS WebGL engine (`visualizer.js`, driven from Blazor via JS interop) warps the product's real texture through a 4-corner homography, clips it to the mask, and multiplies by the photo's own luminance to carry shadows. The photo is processed in memory on the server and never persisted; only the SAM image embedding is cached (15 min) so refinement taps skip the expensive encoder.

**Tech Stack:** ASP.NET Core 8 Web API, EF Core (SQL Server), Microsoft.ML.OnnxRuntime (CPU), SixLabors.ImageSharp, Blazor WebAssembly + Bootstrap 5, plain JavaScript (WebGL1 + canvas-2D fallback), xUnit + EF InMemory (new test project).

**Spec:** `docs/visualizer-specification.md` (v2). Read it before starting.

## Global Constraints

- All UI text in Bulgarian — every label, button, message, tooltip. Docs/code/comments in English.
- API error format is always `{ "error": "Българско съобщение" }` with proper HTTP status codes.
- Controllers are thin — logic lives in `Services/`. Never return EF entities; DTOs only (`record` types, `init` setters on API side).
- Currency display `XX.XX €`; decimals are `decimal(18,2)` / `HasPrecision(18, 2)`.
- C# naming: PascalCase public, `_camelCase` private fields, `var` when obvious, async/await throughout (no `.Result`/`.Wait()`), nullable reference types enabled.
- Admin endpoints get `[Authorize]`; public endpoints (everything the visualizer customer touches) get none.
- Bootstrap 5 classes for styling; custom CSS only where Bootstrap can't do it (the canvas stage needs some).
- The customer photo must NEVER be written to disk on the server and never sent to any third party.
- Commit after every task with the message given in its final step.
- Commands run from the repo root. API runs at `https://localhost:5001`, client at `https://localhost:5002`.
- EF migrations: `dotnet ef migrations add <Name> --project src/NaturalStoneImpex.Api` (does not require a running SQL Server; migrations apply automatically at API startup via `MigrateAsync`).

---

### Task 1: Test project + Product visualizer fields + migration

**Files:**
- Create: `tests/NaturalStoneImpex.Api.Tests/NaturalStoneImpex.Api.Tests.csproj` (scaffolded)
- Create: `tests/NaturalStoneImpex.Api.Tests/ProductVisualizerFieldsTests.cs`
- Modify: `src/NaturalStoneImpex.Api/Models/Entities/Product.cs`
- Modify: `src/NaturalStoneImpex.Api/Data/AppDbContext.cs` (Product entity config block, ~line 38–55)
- Modify: `src/NaturalStoneImpex.Api/Models/DTOs/ProductDto.cs`
- Modify: `src/NaturalStoneImpex.Api/Models/DTOs/CreateProductRequest.cs`
- Modify: `src/NaturalStoneImpex.Api/Models/DTOs/UpdateProductRequest.cs`
- Modify: `src/NaturalStoneImpex.Api/Services/ProductService.cs`

**Interfaces:**
- Produces: `Product.IsVisualizerEnabled` (bool), `Product.TextureImagePath` (string?), `Product.TextureWidthMeters` (decimal, default 1.00m). `ProductDto` gains `IsVisualizerEnabled`, `TextureImagePath`, `TextureWidthMeters`. `CreateProductRequest`/`UpdateProductRequest` gain `IsVisualizerEnabled` (bool) and `TextureWidthMeters` (decimal). Later tasks (2, 13) rely on these exact names.

- [ ] **Step 1: Scaffold the test project**

```powershell
dotnet new xunit -o tests/NaturalStoneImpex.Api.Tests
dotnet add tests/NaturalStoneImpex.Api.Tests reference src/NaturalStoneImpex.Api
dotnet add tests/NaturalStoneImpex.Api.Tests package Microsoft.EntityFrameworkCore.InMemory
dotnet sln add tests/NaturalStoneImpex.Api.Tests
```

If there is no `.sln` at the repo root, create one first: `dotnet new sln -n NaturalStoneImpex; dotnet sln add src/NaturalStoneImpex.Api src/NaturalStoneImpex.Client`.

- [ ] **Step 2: Write the failing test**

Create `tests/NaturalStoneImpex.Api.Tests/ProductVisualizerFieldsTests.cs`:

```csharp
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
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter ProductVisualizerFieldsTests`
Expected: FAIL — compile error `'Product' does not contain a definition for 'IsVisualizerEnabled'`.

- [ ] **Step 4: Add the entity fields**

In `src/NaturalStoneImpex.Api/Models/Entities/Product.cs`, after `public bool IsActive { get; set; } = true;` add:

```csharp
    public bool IsVisualizerEnabled { get; set; }
    public string? TextureImagePath { get; set; }
    public decimal TextureWidthMeters { get; set; } = 1.00m;
```

- [ ] **Step 5: Configure in AppDbContext**

In `src/NaturalStoneImpex.Api/Data/AppDbContext.cs`, inside the `modelBuilder.Entity<Product>(entity => { ... })` block, after the `ImagePath` line add:

```csharp
            entity.Property(e => e.TextureImagePath).HasMaxLength(500);
            entity.Property(e => e.TextureWidthMeters).HasPrecision(18, 2).HasDefaultValue(1.00m);
            entity.HasIndex(e => e.IsVisualizerEnabled);
```

- [ ] **Step 6: Extend the DTOs**

In `src/NaturalStoneImpex.Api/Models/DTOs/ProductDto.cs`, after `public bool IsActive { get; init; }` add:

```csharp
    public bool IsVisualizerEnabled { get; init; }
    public string? TextureImagePath { get; init; }
    public decimal TextureWidthMeters { get; init; }
```

In **both** `CreateProductRequest.cs` and `UpdateProductRequest.cs`, after the `StockQuantity` property add:

```csharp
    public bool IsVisualizerEnabled { get; init; }

    [Range(0.1, 100, ErrorMessage = "Ширината на текстурата трябва да е между 0.1 и 100 метра.")]
    public decimal TextureWidthMeters { get; init; } = 1.00m;
```

- [ ] **Step 7: Map the fields in ProductService**

In `src/NaturalStoneImpex.Api/Services/ProductService.cs`:

1. In `GetByIdAsync`'s `Select(p => new ProductDto { ... })`, add:
```csharp
                IsVisualizerEnabled = p.IsVisualizerEnabled,
                TextureImagePath = p.TextureImagePath,
                TextureWidthMeters = p.TextureWidthMeters,
```
2. In `CreateAsync`, add to the `new Product { ... }` initializer (before `IsActive = true`):
```csharp
            TextureWidthMeters = request.TextureWidthMeters,
```
   and immediately after the duplicate-name check add the guard (a brand-new product cannot have a texture yet):
```csharp
        if (request.IsVisualizerEnabled)
            throw new InvalidOperationException("За да включите продукта във визуализатора, първо качете текстура.");
```
3. In `UpdateAsync`, after `product.StockQuantity = request.StockQuantity;` add:
```csharp
        if (request.IsVisualizerEnabled && string.IsNullOrEmpty(product.TextureImagePath))
            throw new InvalidOperationException("За да включите продукта във визуализатора, първо качете текстура.");

        product.IsVisualizerEnabled = request.IsVisualizerEnabled;
        product.TextureWidthMeters = request.TextureWidthMeters;
```
4. In the `return new ProductDto { ... }` blocks of `CreateAsync` and `UpdateAsync`, add the same three DTO lines as in `GetByIdAsync`.

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter ProductVisualizerFieldsTests`
Expected: PASS (2 tests).

- [ ] **Step 9: Create the migration and build**

```powershell
dotnet ef migrations add AddProductVisualizerFields --project src/NaturalStoneImpex.Api
dotnet build
```
Expected: a new migration appears under `src/NaturalStoneImpex.Api/Migrations/` containing `AddColumn` for `IsVisualizerEnabled`, `TextureImagePath`, `TextureWidthMeters`; build succeeds.

- [ ] **Step 10: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): add product texture fields and test project"
```

---

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

### Task 3: ONNX runtime + MobileSAM model wrapper

**Files:**
- Modify: `src/NaturalStoneImpex.Api/NaturalStoneImpex.Api.csproj`
- Create: `scripts/download-visualizer-models.ps1`
- Create: `src/NaturalStoneImpex.Api/MLModels/.gitkeep`
- Modify: `.gitignore` (repo root)
- Create: `src/NaturalStoneImpex.Api/Services/Segmentation/ISamModel.cs`
- Create: `src/NaturalStoneImpex.Api/Services/Segmentation/SamOnnxModel.cs`
- Test: `tests/NaturalStoneImpex.Api.Tests/SamOnnxModelTests.cs`

**Interfaces:**
- Produces (Tasks 5–6 depend on these exact signatures):

```csharp
public record SamPoint(float X, float Y, int Label);                    // Label: 1 = add, 0 = remove
public record SamEmbedding(float[] Data, float Scale, int OrigWidth, int OrigHeight);
public interface ISamModel
{
    bool IsAvailable { get; }
    SamEmbedding Encode(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24> image);
    float[,] Decode(SamEmbedding embedding, IReadOnlyList<SamPoint> points); // [height, width] logits, >0 = selected
}
```

- [ ] **Step 1: Add packages and model folder**

```powershell
dotnet add src/NaturalStoneImpex.Api package Microsoft.ML.OnnxRuntime
dotnet add src/NaturalStoneImpex.Api package SixLabors.ImageSharp
New-Item -ItemType Directory -Force src/NaturalStoneImpex.Api/MLModels
New-Item -ItemType File src/NaturalStoneImpex.Api/MLModels/.gitkeep
```

Append to the repo-root `.gitignore`:

```
# Visualizer ONNX models (downloaded via scripts/download-visualizer-models.ps1)
src/NaturalStoneImpex.Api/MLModels/*.onnx
```

Also add to `src/NaturalStoneImpex.Api/NaturalStoneImpex.Api.csproj` (inside `<Project>`, after the existing `<ItemGroup>`) so models deploy with `dotnet publish`:

```xml
  <ItemGroup>
    <Content Include="MLModels\*.onnx" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 2: Create the model download script**

The pre-exported MobileSAM ONNX files live in the Hugging Face repo `vietanhdev/segment-anything-onnx-models` (from the `samexporter` project, Apache-2.0). **First open https://huggingface.co/vietanhdev/segment-anything-onnx-models/tree/main in a browser and note the exact MobileSAM encoder/decoder file names** (e.g., `mobile_sam.encoder.onnx` / `mobile_sam.decoder.onnx` — names may differ or be zipped), then set them in the variables below. Create `scripts/download-visualizer-models.ps1`:

```powershell
# Downloads the MobileSAM ONNX models used by the product visualizer.
# Verify file names at https://huggingface.co/vietanhdev/segment-anything-onnx-models/tree/main
$repo = "https://huggingface.co/vietanhdev/segment-anything-onnx-models/resolve/main"
$encoderFile = "mobile_sam.encoder.onnx"   # <-- confirm against the repo listing
$decoderFile = "mobile_sam.decoder.onnx"   # <-- confirm against the repo listing
$target = Join-Path $PSScriptRoot "..\src\NaturalStoneImpex.Api\MLModels"

Invoke-WebRequest "$repo/$encoderFile" -OutFile (Join-Path $target "mobilesam-encoder.onnx")
Invoke-WebRequest "$repo/$decoderFile" -OutFile (Join-Path $target "mobilesam-decoder.onnx")
Write-Host "Models downloaded to $target"
```

Run it: `powershell -File scripts/download-visualizer-models.ps1`. Expected: two `.onnx` files in `src/NaturalStoneImpex.Api/MLModels/` (encoder tens of MB, decoder a few MB). Fallback if the repo layout changed: `pip install samexporter` and export from the MobileSAM checkpoint per https://github.com/vietanhdev/samexporter#usage, then copy the outputs to the same target names.

- [ ] **Step 3: Write the failing integration test**

Create `tests/NaturalStoneImpex.Api.Tests/SamOnnxModelTests.cs`. The test builds a synthetic photo (gray "driveway" rectangle on green background), taps its center, and expects a mask that covers the tap but not the whole image. It skips silently when model files are absent (CI without models):

```csharp
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Services.Segmentation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NaturalStoneImpex.Api.Tests;

public class SamOnnxModelTests
{
    private static string ModelsDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "NaturalStoneImpex.Api", "MLModels"));

    [Fact]
    public void Encode_and_decode_segments_the_tapped_region()
    {
        var encoderPath = Path.Combine(ModelsDir, "mobilesam-encoder.onnx");
        var decoderPath = Path.Combine(ModelsDir, "mobilesam-decoder.onnx");
        if (!File.Exists(encoderPath) || !File.Exists(decoderPath))
            return; // models not downloaded — skip (see scripts/download-visualizer-models.ps1)

        using var image = new Image<Rgb24>(1024, 768, new Rgb24(60, 140, 60));
        image.Mutate(ctx => ctx.Fill(new Rgb24(128, 126, 124),
            new RectangularPolygon(300, 400, 600, 300))); // gray rectangle = "driveway"

        var model = new SamOnnxModel(encoderPath, decoderPath);
        Assert.True(model.IsAvailable);

        var embedding = model.Encode(image);
        Assert.Equal(1024, embedding.OrigWidth);
        Assert.Equal(768, embedding.OrigHeight);

        var logits = model.Decode(embedding, new[] { new SamPoint(600f, 550f, 1) });
        Assert.Equal(768, logits.GetLength(0));
        Assert.Equal(1024, logits.GetLength(1));
        Assert.True(logits[550, 600] > 0, "tapped pixel must be selected");

        var selected = 0;
        for (var y = 0; y < 768; y++)
            for (var x = 0; x < 1024; x++)
                if (logits[y, x] > 0) selected++;
        var fraction = selected / (768.0 * 1024.0);
        Assert.InRange(fraction, 0.02, 0.90); // some region, not the whole image
    }
}
```

Add the drawing package the test uses: `dotnet add tests/NaturalStoneImpex.Api.Tests package SixLabors.ImageSharp.Drawing`

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter SamOnnxModelTests`
Expected: FAIL — `SamOnnxModel` / `SamPoint` do not exist.

- [ ] **Step 5: Implement the interface and wrapper**

Create `src/NaturalStoneImpex.Api/Services/Segmentation/ISamModel.cs`:

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NaturalStoneImpex.Api.Services.Segmentation;

/// <summary>A point prompt in original-photo pixel coordinates. Label: 1 = include, 0 = exclude.</summary>
public record SamPoint(float X, float Y, int Label);

/// <summary>Encoder output plus the geometry needed to run the decoder. ~4 MB per photo.</summary>
public record SamEmbedding(float[] Data, float Scale, int OrigWidth, int OrigHeight);

public interface ISamModel
{
    bool IsAvailable { get; }
    SamEmbedding Encode(Image<Rgb24> image);
    /// <returns>Mask logits [OrigHeight, OrigWidth]; values &gt; 0 are selected.</returns>
    float[,] Decode(SamEmbedding embedding, IReadOnlyList<SamPoint> points);
}
```

Create `src/NaturalStoneImpex.Api/Services/Segmentation/SamOnnxModel.cs`:

```csharp
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NaturalStoneImpex.Api.Services.Segmentation;

/// <summary>
/// MobileSAM via ONNX Runtime on CPU. Standard SAM contract: images are resized so the
/// long side is 1024, padded bottom/right to 1024x1024, normalized with ImageNet mean/std.
/// Point prompts are given in the 1024-scale space; an extra (0,0) point with label -1
/// is appended as the "no box" padding prompt required by SAM decoders.
/// InferenceSession.Run is thread-safe; register this class as a singleton.
/// </summary>
public class SamOnnxModel : ISamModel, IDisposable
{
    private const int InputSize = 1024;
    private static readonly float[] Mean = { 123.675f, 116.28f, 103.53f };
    private static readonly float[] Std = { 58.395f, 57.12f, 57.375f };

    private readonly InferenceSession? _encoder;
    private readonly InferenceSession? _decoder;

    public bool IsAvailable => _encoder is not null && _decoder is not null;

    public SamOnnxModel(string encoderPath, string decoderPath)
    {
        if (File.Exists(encoderPath) && File.Exists(decoderPath))
        {
            _encoder = new InferenceSession(encoderPath);
            _decoder = new InferenceSession(decoderPath);
        }
    }

    public SamEmbedding Encode(Image<Rgb24> image)
    {
        if (_encoder is null) throw new InvalidOperationException("Encoder model not loaded.");

        var origW = image.Width;
        var origH = image.Height;
        var scale = InputSize / (float)Math.Max(origW, origH);
        var scaledW = (int)Math.Round(origW * scale);
        var scaledH = (int)Math.Round(origH * scale);

        using var resized = image.Clone(ctx => ctx.Resize(scaledW, scaledH));
        var tensor = new DenseTensor<float>(new[] { 1, 3, InputSize, InputSize });
        resized.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    tensor[0, 0, y, x] = (row[x].R - Mean[0]) / Std[0];
                    tensor[0, 1, y, x] = (row[x].G - Mean[1]) / Std[1];
                    tensor[0, 2, y, x] = (row[x].B - Mean[2]) / Std[2];
                }
            }
        });

        var inputName = _encoder.InputMetadata.Keys.First();
        using var results = _encoder.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) });
        var embedding = results.First().AsEnumerable<float>().ToArray();
        return new SamEmbedding(embedding, scale, origW, origH);
    }

    public float[,] Decode(SamEmbedding embedding, IReadOnlyList<SamPoint> points)
    {
        if (_decoder is null) throw new InvalidOperationException("Decoder model not loaded.");

        var n = points.Count + 1; // + padding point
        var coords = new DenseTensor<float>(new[] { 1, n, 2 });
        var labels = new DenseTensor<float>(new[] { 1, n });
        for (var i = 0; i < points.Count; i++)
        {
            coords[0, i, 0] = points[i].X * embedding.Scale;
            coords[0, i, 1] = points[i].Y * embedding.Scale;
            labels[0, i] = points[i].Label;
        }
        coords[0, n - 1, 0] = 0f;
        coords[0, n - 1, 1] = 0f;
        labels[0, n - 1] = -1f;

        var embeddingTensor = new DenseTensor<float>(embedding.Data, new[] { 1, 256, 64, 64 });
        var maskInput = new DenseTensor<float>(new[] { 1, 1, 256, 256 });
        var hasMask = new DenseTensor<float>(new[] { 0f }, new[] { 1 });
        var origSize = new DenseTensor<float>(
            new[] { (float)embedding.OrigHeight, embedding.OrigWidth }, new[] { 2 });

        var candidates = new Dictionary<string, NamedOnnxValue>
        {
            ["image_embeddings"] = NamedOnnxValue.CreateFromTensor("image_embeddings", embeddingTensor),
            ["point_coords"] = NamedOnnxValue.CreateFromTensor("point_coords", coords),
            ["point_labels"] = NamedOnnxValue.CreateFromTensor("point_labels", labels),
            ["mask_input"] = NamedOnnxValue.CreateFromTensor("mask_input", maskInput),
            ["has_mask_input"] = NamedOnnxValue.CreateFromTensor("has_mask_input", hasMask),
            ["orig_im_size"] = NamedOnnxValue.CreateFromTensor("orig_im_size", origSize)
        };
        // Feed only the inputs this particular export declares.
        var inputs = _decoder.InputMetadata.Keys
            .Where(candidates.ContainsKey)
            .Select(k => candidates[k])
            .ToList();

        using var results = _decoder.Run(inputs);
        var masks = results.First(r => r.Name is "masks" or "low_res_masks");
        var tensor = (DenseTensor<float>)masks.AsTensor<float>();
        var dims = tensor.Dimensions.ToArray();
        var h = dims[^2];
        var w = dims[^1];

        if (h == embedding.OrigHeight && w == embedding.OrigWidth)
        {
            var mask = new float[h, w];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    mask[y, x] = tensor[0, 0, y, x];
            return mask;
        }

        // Export returned low-res (e.g., 256x256) logits — upscale bilinearly to photo size.
        return BilinearResize(tensor, h, w, embedding.OrigHeight, embedding.OrigWidth,
            embedding.Scale, embedding.OrigWidth, embedding.OrigHeight);
    }

    private static float[,] BilinearResize(DenseTensor<float> src, int srcH, int srcW,
        int dstH, int dstW, float scale, int origW, int origH)
    {
        // Low-res SAM masks correspond to the 1024x1024 padded frame; only the
        // (origW*scale) x (origH*scale) top-left region is valid.
        var validW = origW * scale / 1024f * srcW;
        var validH = origH * scale / 1024f * srcH;
        var result = new float[dstH, dstW];
        for (var y = 0; y < dstH; y++)
        {
            var sy = Math.Clamp(y / (float)dstH * validH, 0, srcH - 1.001f);
            int y0 = (int)sy; var fy = sy - y0;
            for (var x = 0; x < dstW; x++)
            {
                var sx = Math.Clamp(x / (float)dstW * validW, 0, srcW - 1.001f);
                int x0 = (int)sx; var fx = sx - x0;
                var top = src[0, 0, y0, x0] * (1 - fx) + src[0, 0, y0, x0 + 1] * fx;
                var bottom = src[0, 0, y0 + 1, x0] * (1 - fx) + src[0, 0, y0 + 1, x0 + 1] * fx;
                result[y, x] = top * (1 - fy) + bottom * fy;
            }
        }
        return result;
    }

    public void Dispose()
    {
        _encoder?.Dispose();
        _decoder?.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 6: Run the test; inspect the model contract if it fails**

Run: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter SamOnnxModelTests`
Expected: PASS (takes a few seconds — CPU encoding).
If it fails on tensor names/shapes, print the actual contract and adapt `Encode`/`Decode` input/output names to it:

```csharp
// temporary diagnostic inside the test:
foreach (var kv in model /* expose sessions temporarily */) { }
// or simpler: new InferenceSession(path).InputMetadata / .OutputMetadata in a scratch test,
// printing kv.Key, kv.Value.ElementType, string.Join(",", kv.Value.Dimensions)
```

Document any deviation in a comment at the top of `SamOnnxModel.cs`.

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): MobileSAM ONNX wrapper with CPU inference"
```

---

### Task 4: Mask post-processing

**Files:**
- Create: `src/NaturalStoneImpex.Api/Services/Segmentation/MaskPostProcessor.cs`
- Test: `tests/NaturalStoneImpex.Api.Tests/MaskPostProcessorTests.cs`

**Interfaces:**
- Produces (Task 5 depends on these):

```csharp
public static class MaskPostProcessor
{
    public static bool[,] Threshold(float[,] logits, float threshold = 0f);
    public static bool[,] KeepComponentsContaining(bool[,] mask, IEnumerable<(int X, int Y)> seeds);
    public static bool[,] MorphClose(bool[,] mask, int radius = 2);
    public static bool[,] MorphOpen(bool[,] mask, int radius = 1);
    public static byte[] ToPng(bool[,] mask); // 8-bit grayscale PNG, white = selected
}
```

- [ ] **Step 1: Write the failing tests**

Create `tests/NaturalStoneImpex.Api.Tests/MaskPostProcessorTests.cs`:

```csharp
using NaturalStoneImpex.Api.Services.Segmentation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NaturalStoneImpex.Api.Tests;

public class MaskPostProcessorTests
{
    private static bool[,] Blank(int h, int w) => new bool[h, w];

    private static bool[,] WithRect(bool[,] mask, int x0, int y0, int x1, int y1)
    {
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
                mask[y, x] = true;
        return mask;
    }

    [Fact]
    public void Threshold_selects_positive_logits()
    {
        var logits = new float[2, 2] { { -1f, 0.5f }, { 0f, 3f } };
        var mask = MaskPostProcessor.Threshold(logits);
        Assert.False(mask[0, 0]);
        Assert.True(mask[0, 1]);
        Assert.False(mask[1, 0]); // 0 is not > 0
        Assert.True(mask[1, 1]);
    }

    [Fact]
    public void KeepComponentsContaining_removes_untouched_blobs()
    {
        var mask = WithRect(WithRect(Blank(50, 100), 5, 5, 20, 20), 60, 30, 90, 45);
        var result = MaskPostProcessor.KeepComponentsContaining(mask, new[] { (10, 10) });
        Assert.True(result[10, 10]);   // seeded blob stays
        Assert.False(result[35, 70]);  // other blob removed
    }

    [Fact]
    public void MorphClose_fills_small_holes()
    {
        var mask = WithRect(Blank(30, 30), 5, 5, 24, 24);
        mask[15, 15] = false; // 1px hole
        var result = MaskPostProcessor.MorphClose(mask, 2);
        Assert.True(result[15, 15]);
    }

    [Fact]
    public void MorphOpen_removes_speckles()
    {
        var mask = Blank(30, 30);
        mask[10, 10] = true; // isolated pixel
        var result = MaskPostProcessor.MorphOpen(mask, 1);
        Assert.False(result[10, 10]);
    }

    [Fact]
    public void ToPng_roundtrips_white_selected_black_rest()
    {
        var mask = WithRect(Blank(10, 10), 2, 2, 5, 5);
        var png = MaskPostProcessor.ToPng(mask);
        using var image = Image.Load<L8>(png);
        Assert.Equal(10, image.Width);
        Assert.Equal(255, image[3, 3].PackedValue);
        Assert.Equal(0, image[8, 8].PackedValue);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter MaskPostProcessorTests`
Expected: FAIL — `MaskPostProcessor` does not exist.

- [ ] **Step 3: Implement**

Create `src/NaturalStoneImpex.Api/Services/Segmentation/MaskPostProcessor.cs`:

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace NaturalStoneImpex.Api.Services.Segmentation;

/// <summary>Binary-mask cleanup between the SAM decoder and the client:
/// threshold → keep only tapped components → close small holes → drop speckles → PNG.</summary>
public static class MaskPostProcessor
{
    public static bool[,] Threshold(float[,] logits, float threshold = 0f)
    {
        var h = logits.GetLength(0);
        var w = logits.GetLength(1);
        var mask = new bool[h, w];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                mask[y, x] = logits[y, x] > threshold;
        return mask;
    }

    public static bool[,] KeepComponentsContaining(bool[,] mask, IEnumerable<(int X, int Y)> seeds)
    {
        var h = mask.GetLength(0);
        var w = mask.GetLength(1);
        var result = new bool[h, w];
        var queue = new Queue<(int X, int Y)>();

        foreach (var (sx, sy) in seeds)
        {
            if (sx < 0 || sy < 0 || sx >= w || sy >= h) continue;
            if (mask[sy, sx] && !result[sy, sx])
            {
                result[sy, sx] = true;
                queue.Enqueue((sx, sy));
            }
        }

        Span<(int dx, int dy)> dirs = stackalloc[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (dx, dy) in dirs)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < w && ny < h && mask[ny, nx] && !result[ny, nx])
                {
                    result[ny, nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }
        return result;
    }

    public static bool[,] MorphClose(bool[,] mask, int radius = 2) => Erode(Dilate(mask, radius), radius);

    public static bool[,] MorphOpen(bool[,] mask, int radius = 1) => Dilate(Erode(mask, radius), radius);

    public static byte[] ToPng(bool[,] mask)
    {
        var h = mask.GetLength(0);
        var w = mask.GetLength(1);
        using var image = new Image<L8>(w, h);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < h; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < w; x++)
                    row[x] = new L8(mask[y, x] ? (byte)255 : (byte)0);
            }
        });
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    private static bool[,] Dilate(bool[,] mask, int radius) => BoxPass(mask, radius, any: true);

    private static bool[,] Erode(bool[,] mask, int radius) => BoxPass(mask, radius, any: false);

    /// <summary>Separable box morphology: horizontal pass then vertical pass.
    /// any=true → dilation (true if any neighbor set); any=false → erosion (true if all set).</summary>
    private static bool[,] BoxPass(bool[,] mask, int radius, bool any)
    {
        var h = mask.GetLength(0);
        var w = mask.GetLength(1);
        var horizontal = new bool[h, w];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var value = !any;
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var nx = x + dx;
                    var sample = nx >= 0 && nx < w ? mask[y, nx] : !any;
                    if (any) value |= sample; else value &= sample;
                }
                horizontal[y, x] = value;
            }

        var result = new bool[h, w];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var value = !any;
                for (var dy = -radius; dy <= radius; dy++)
                {
                    var ny = y + dy;
                    var sample = ny >= 0 && ny < h ? horizontal[ny, x] : !any;
                    if (any) value |= sample; else value &= sample;
                }
                result[y, x] = value;
            }
        return result;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter MaskPostProcessorTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): mask post-processing pipeline"
```

---

### Task 5: SegmentationService + quotas + VisualizationRequest entity

**Files:**
- Create: `src/NaturalStoneImpex.Api/Models/Entities/VisualizationRequest.cs`
- Modify: `src/NaturalStoneImpex.Api/Data/AppDbContext.cs`
- Create: `src/NaturalStoneImpex.Api/Services/Segmentation/VisualizerOptions.cs`
- Create: `src/NaturalStoneImpex.Api/Services/Segmentation/EncodeGate.cs`
- Create: `src/NaturalStoneImpex.Api/Services/Segmentation/ISegmentationService.cs`
- Create: `src/NaturalStoneImpex.Api/Services/Segmentation/SegmentationService.cs`
- Test: `tests/NaturalStoneImpex.Api.Tests/SegmentationServiceTests.cs`

**Interfaces:**
- Consumes: `ISamModel`, `SamPoint`, `SamEmbedding` (Task 3); `MaskPostProcessor` (Task 4).
- Produces (Task 6 depends on these):

```csharp
public record SegmentResult(string SessionToken, string MaskPng, int Width, int Height); // MaskPng = base64
public record SegmentOutcome(int StatusCode, string? Error, SegmentResult? Result);      // 200/400/404/429/503
public interface ISegmentationService
{
    Task<SegmentOutcome> SegmentNewAsync(Stream photo, IReadOnlyList<SamPoint> points, string clientIp);
    Task<SegmentOutcome> RefineAsync(string sessionToken, IReadOnlyList<SamPoint> points);
}
public class VisualizerOptions
{
    public bool Enabled { get; set; } = true;
    public string EncoderPath { get; set; } = "MLModels/mobilesam-encoder.onnx";
    public string DecoderPath { get; set; } = "MLModels/mobilesam-decoder.onnx";
    public long MaxUploadBytes { get; set; } = 10_485_760;
    public int MaxImageDimension { get; set; } = 2048;
    public int MaxConcurrentEncodes { get; set; } = 2;
    public int EmbeddingCacheMinutes { get; set; } = 15;
    public int PerIpDailyLimit { get; set; } = 20;
    public int GlobalDailyLimit { get; set; } = 500;
}
```

- [ ] **Step 1: Add the entity and DbContext config**

Create `src/NaturalStoneImpex.Api/Models/Entities/VisualizationRequest.cs`:

```csharp
namespace NaturalStoneImpex.Api.Models.Entities;

public enum VisualizationStatus
{
    Succeeded = 0,
    Failed = 1
}

/// <summary>Quota/telemetry row per uploaded photo. Contains no personal data:
/// IpHash is SHA-256 of (ip + day), no photos or results are ever stored.</summary>
public class VisualizationRequest
{
    public int Id { get; set; }
    public string IpHash { get; set; } = string.Empty;
    public VisualizationStatus Status { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

In `src/NaturalStoneImpex.Api/Data/AppDbContext.cs` add the DbSet after `InvoiceItems`:

```csharp
    public DbSet<VisualizationRequest> VisualizationRequests => Set<VisualizationRequest>();
```

and at the end of `OnModelCreating`:

```csharp
        modelBuilder.Entity<VisualizationRequest>(entity =>
        {
            entity.Property(e => e.IpHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.IpHash, e.CreatedAt });
            entity.HasIndex(e => e.CreatedAt);
        });
```

- [ ] **Step 2: Write the failing tests**

Create `tests/NaturalStoneImpex.Api.Tests/SegmentationServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Services.Segmentation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NaturalStoneImpex.Api.Tests;

public class FakeSamModel : ISamModel
{
    public bool IsAvailable => true;
    public int EncodeCalls;

    public SamEmbedding Encode(Image<Rgb24> image)
    {
        EncodeCalls++;
        return new SamEmbedding(new float[256 * 64 * 64], 1024f / Math.Max(image.Width, image.Height),
            image.Width, image.Height);
    }

    public float[,] Decode(SamEmbedding embedding, IReadOnlyList<SamPoint> points)
    {
        // A 100x100 positive square around the first point.
        var logits = new float[embedding.OrigHeight, embedding.OrigWidth];
        for (var y = 0; y < embedding.OrigHeight; y++)
            for (var x = 0; x < embedding.OrigWidth; x++)
                logits[y, x] = -10f;
        var px = (int)points[0].X;
        var py = (int)points[0].Y;
        for (var y = Math.Max(0, py - 50); y < Math.Min(embedding.OrigHeight, py + 50); y++)
            for (var x = Math.Max(0, px - 50); x < Math.Min(embedding.OrigWidth, px + 50); x++)
                logits[y, x] = 10f;
        return logits;
    }
}

public class SegmentationServiceTests
{
    private static byte[] TestPhotoBytes()
    {
        using var image = new Image<Rgb24>(400, 300, new Rgb24(100, 100, 100));
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    private static (SegmentationService Service, FakeSamModel Model, AppDbContext Db) CreateService(
        VisualizerOptions? options = null)
    {
        options ??= new VisualizerOptions();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var model = new FakeSamModel();
        var service = new SegmentationService(model, db,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(options), new EncodeGate(Options.Create(options)));
        return (service, model, db);
    }

    private static List<SamPoint> Tap() => new() { new SamPoint(200f, 150f, 1) };

    [Fact]
    public async Task Happy_path_returns_token_and_mask()
    {
        var (service, _, _) = CreateService();
        using var photo = new MemoryStream(TestPhotoBytes());

        var outcome = await service.SegmentNewAsync(photo, Tap(), "1.2.3.4");

        Assert.Equal(200, outcome.StatusCode);
        Assert.NotNull(outcome.Result);
        Assert.False(string.IsNullOrEmpty(outcome.Result!.SessionToken));
        Assert.False(string.IsNullOrEmpty(outcome.Result.MaskPng));
        Assert.Equal(400, outcome.Result.Width);
        Assert.Equal(300, outcome.Result.Height);
    }

    [Fact]
    public async Task Refine_reuses_cached_embedding_without_reencoding()
    {
        var (service, model, _) = CreateService();
        using var photo = new MemoryStream(TestPhotoBytes());
        var first = await service.SegmentNewAsync(photo, Tap(), "1.2.3.4");

        var refined = await service.RefineAsync(first.Result!.SessionToken, Tap());

        Assert.Equal(200, refined.StatusCode);
        Assert.Equal(1, model.EncodeCalls);
    }

    [Fact]
    public async Task Refine_with_unknown_token_returns_404()
    {
        var (service, _, _) = CreateService();
        var outcome = await service.RefineAsync(Guid.NewGuid().ToString("N"), Tap());
        Assert.Equal(404, outcome.StatusCode);
        Assert.Equal("Сесията е изтекла. Моля, качете снимката отново.", outcome.Error);
    }

    [Fact]
    public async Task PerIp_quota_blocks_with_429()
    {
        var (service, _, _) = CreateService(new VisualizerOptions { PerIpDailyLimit = 2 });
        for (var i = 0; i < 2; i++)
        {
            using var photo = new MemoryStream(TestPhotoBytes());
            Assert.Equal(200, (await service.SegmentNewAsync(photo, Tap(), "5.5.5.5")).StatusCode);
        }

        using var third = new MemoryStream(TestPhotoBytes());
        var blocked = await service.SegmentNewAsync(third, Tap(), "5.5.5.5");

        Assert.Equal(429, blocked.StatusCode);
        Assert.Equal("Достигнахте дневния лимит за визуализации. Опитайте отново утре.", blocked.Error);
    }

    [Fact]
    public async Task Disabled_feature_returns_503()
    {
        var (service, _, _) = CreateService(new VisualizerOptions { Enabled = false });
        using var photo = new MemoryStream(TestPhotoBytes());
        var outcome = await service.SegmentNewAsync(photo, Tap(), "1.2.3.4");
        Assert.Equal(503, outcome.StatusCode);
    }

    [Fact]
    public async Task Invalid_image_returns_400()
    {
        var (service, _, _) = CreateService();
        using var junk = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var outcome = await service.SegmentNewAsync(junk, Tap(), "1.2.3.4");
        Assert.Equal(400, outcome.StatusCode);
        Assert.Equal("Моля, качете снимка във формат JPG или PNG до 10 MB.", outcome.Error);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter SegmentationServiceTests`
Expected: FAIL — `SegmentationService`, `VisualizerOptions`, `EncodeGate` do not exist.

- [ ] **Step 4: Implement options, gate, interface, and service**

Create `src/NaturalStoneImpex.Api/Services/Segmentation/VisualizerOptions.cs` with the class exactly as shown in the Interfaces block above (namespace `NaturalStoneImpex.Api.Services.Segmentation`).

Create `src/NaturalStoneImpex.Api/Services/Segmentation/EncodeGate.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace NaturalStoneImpex.Api.Services.Segmentation;

/// <summary>Singleton semaphore bounding concurrent CPU-heavy encoder runs.</summary>
public class EncodeGate
{
    public SemaphoreSlim Semaphore { get; }

    public EncodeGate(IOptions<VisualizerOptions> options)
    {
        Semaphore = new SemaphoreSlim(options.Value.MaxConcurrentEncodes);
    }
}
```

Create `src/NaturalStoneImpex.Api/Services/Segmentation/ISegmentationService.cs`:

```csharp
namespace NaturalStoneImpex.Api.Services.Segmentation;

public record SegmentResult(string SessionToken, string MaskPng, int Width, int Height);

public record SegmentOutcome(int StatusCode, string? Error, SegmentResult? Result)
{
    public static SegmentOutcome Ok(SegmentResult result) => new(200, null, result);
    public static SegmentOutcome Fail(int statusCode, string error) => new(statusCode, error, null);
}

public interface ISegmentationService
{
    Task<SegmentOutcome> SegmentNewAsync(Stream photo, IReadOnlyList<SamPoint> points, string clientIp);
    Task<SegmentOutcome> RefineAsync(string sessionToken, IReadOnlyList<SamPoint> points);
}
```

Create `src/NaturalStoneImpex.Api/Services/Segmentation/SegmentationService.cs`:

```csharp
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NaturalStoneImpex.Api.Services.Segmentation;

public class SegmentationService : ISegmentationService
{
    private const string ErrorUnavailable = "Визуализаторът е временно недостъпен.";
    private const string ErrorBusy = "В момента има много заявки. Опитайте отново след малко.";
    private const string ErrorQuota = "Достигнахте дневния лимит за визуализации. Опитайте отново утре.";
    private const string ErrorGlobalQuota = "Визуализаторът е временно недостъпен. Моля, опитайте по-късно.";
    private const string ErrorBadImage = "Моля, качете снимка във формат JPG или PNG до 10 MB.";
    private const string ErrorExpired = "Сесията е изтекла. Моля, качете снимката отново.";
    private const string ErrorNoSurface = "Не разпознахме повърхност тук. Опитайте друго място или използвайте четката.";

    private readonly ISamModel _model;
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly VisualizerOptions _options;
    private readonly EncodeGate _gate;

    public SegmentationService(ISamModel model, AppDbContext context, IMemoryCache cache,
        IOptions<VisualizerOptions> options, EncodeGate gate)
    {
        _model = model;
        _context = context;
        _cache = cache;
        _options = options.Value;
        _gate = gate;
    }

    public async Task<SegmentOutcome> SegmentNewAsync(Stream photo, IReadOnlyList<SamPoint> points, string clientIp)
    {
        if (!_options.Enabled || !_model.IsAvailable)
            return SegmentOutcome.Fail(503, ErrorUnavailable);

        var today = DateTime.UtcNow.Date;
        var ipHash = HashIp(clientIp);
        var ipCount = await _context.VisualizationRequests
            .CountAsync(r => r.IpHash == ipHash && r.CreatedAt >= today);
        if (ipCount >= _options.PerIpDailyLimit)
            return SegmentOutcome.Fail(429, ErrorQuota);

        var globalCount = await _context.VisualizationRequests.CountAsync(r => r.CreatedAt >= today);
        if (globalCount >= _options.GlobalDailyLimit)
            return SegmentOutcome.Fail(429, ErrorGlobalQuota);

        Image<Rgb24> image;
        try
        {
            // Fully in-memory: the photo is never written to disk.
            image = await Image.LoadAsync<Rgb24>(photo);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            return SegmentOutcome.Fail(400, ErrorBadImage);
        }

        using (image)
        {
            if (Math.Max(image.Width, image.Height) > _options.MaxImageDimension)
            {
                var factor = _options.MaxImageDimension / (double)Math.Max(image.Width, image.Height);
                image.Mutate(ctx => ctx.Resize(
                    (int)Math.Round(image.Width * factor), (int)Math.Round(image.Height * factor)));
            }

            if (!await _gate.Semaphore.WaitAsync(TimeSpan.FromSeconds(30)))
                return SegmentOutcome.Fail(503, ErrorBusy);

            SamEmbedding embedding;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                embedding = _model.Encode(image);
            }
            finally
            {
                _gate.Semaphore.Release();
            }

            var token = Guid.NewGuid().ToString("N");
            _cache.Set(CacheKey(token), embedding, new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = TimeSpan.FromMinutes(_options.EmbeddingCacheMinutes)
            });

            _context.VisualizationRequests.Add(new VisualizationRequest
            {
                IpHash = ipHash,
                Status = VisualizationStatus.Succeeded,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return BuildOutcome(token, embedding, points);
        }
    }

    public Task<SegmentOutcome> RefineAsync(string sessionToken, IReadOnlyList<SamPoint> points)
    {
        if (!_options.Enabled || !_model.IsAvailable)
            return Task.FromResult(SegmentOutcome.Fail(503, ErrorUnavailable));

        if (!_cache.TryGetValue(CacheKey(sessionToken), out SamEmbedding? embedding) || embedding is null)
            return Task.FromResult(SegmentOutcome.Fail(404, ErrorExpired));

        return Task.FromResult(BuildOutcome(sessionToken, embedding, points));
    }

    private SegmentOutcome BuildOutcome(string token, SamEmbedding embedding, IReadOnlyList<SamPoint> points)
    {
        var logits = _model.Decode(embedding, points);
        var mask = MaskPostProcessor.Threshold(logits);
        var seeds = points.Where(p => p.Label == 1).Select(p => ((int)p.X, (int)p.Y));
        mask = MaskPostProcessor.KeepComponentsContaining(mask, seeds);
        mask = MaskPostProcessor.MorphClose(mask, 2);
        mask = MaskPostProcessor.MorphOpen(mask, 1);

        var anySelected = false;
        for (var y = 0; y < mask.GetLength(0) && !anySelected; y++)
            for (var x = 0; x < mask.GetLength(1); x++)
                if (mask[y, x]) { anySelected = true; break; }
        if (!anySelected)
            return SegmentOutcome.Fail(400, ErrorNoSurface); // spec §3.4: tap hit no recognizable surface

        var png = MaskPostProcessor.ToPng(mask);
        return SegmentOutcome.Ok(new SegmentResult(token, Convert.ToBase64String(png),
            embedding.OrigWidth, embedding.OrigHeight));
    }

    private static string CacheKey(string token) => $"viz-embedding:{token}";

    private static string HashIp(string ip)
    {
        var input = $"{ip}:{DateTime.UtcNow:yyyyMMdd}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter SegmentationServiceTests`
Expected: PASS (6 tests).

- [ ] **Step 6: Create the migration**

```powershell
dotnet ef migrations add AddVisualizationRequests --project src/NaturalStoneImpex.Api
dotnet build
```
Expected: migration with `CreateTable` for `VisualizationRequests`; build succeeds.

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): segmentation service with quotas and embedding cache"
```

---

### Task 6: Segment endpoints + configuration + DI wiring

**Files:**
- Create: `src/NaturalStoneImpex.Api/Models/DTOs/SegmentPointDto.cs`
- Create: `src/NaturalStoneImpex.Api/Models/DTOs/SegmentResponse.cs`
- Modify: `src/NaturalStoneImpex.Api/Controllers/VisualizerController.cs`
- Modify: `src/NaturalStoneImpex.Api/Program.cs`
- Modify: `src/NaturalStoneImpex.Api/appsettings.json`

**Interfaces:**
- Consumes: `ISegmentationService`, `SamPoint`, `VisualizerOptions`, `EncodeGate`, `ISamModel`/`SamOnnxModel` (Tasks 3, 5).
- Produces (client Task 9 depends on these):
  - `POST /api/visualizer/segment` — multipart form: `photo` (file) + `points` (JSON string `[{"x":123.4,"y":56.7,"label":1}]`) → `200 { "sessionToken", "maskPng", "width", "height" }` (camelCase), or 400/429/503 `{ "error": "..." }`.
  - `POST /api/visualizer/segment/{sessionToken}` — JSON body `[{"x":..,"y":..,"label":..}]` → same 200 shape, or 404/503.

- [ ] **Step 1: Create the DTOs**

Create `src/NaturalStoneImpex.Api/Models/DTOs/SegmentPointDto.cs`:

```csharp
namespace NaturalStoneImpex.Api.Models.DTOs;

/// <summary>A tap point in original-photo pixel coordinates. Label: 1 = add, 0 = remove.</summary>
public record SegmentPointDto(double X, double Y, int Label);
```

Create `src/NaturalStoneImpex.Api/Models/DTOs/SegmentResponse.cs`:

```csharp
namespace NaturalStoneImpex.Api.Models.DTOs;

public record SegmentResponse
{
    public string SessionToken { get; init; } = string.Empty;
    public string MaskPng { get; init; } = string.Empty; // base64 grayscale PNG, white = selected
    public int Width { get; init; }
    public int Height { get; init; }
}
```

- [ ] **Step 2: Add the endpoints to VisualizerController**

Replace the content of `src/NaturalStoneImpex.Api/Controllers/VisualizerController.cs` with:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Services;
using NaturalStoneImpex.Api.Services.Segmentation;

namespace NaturalStoneImpex.Api.Controllers;

[ApiController]
[Route("api/visualizer")]
public class VisualizerController : ControllerBase
{
    private static readonly JsonSerializerOptions PointsJson = new(JsonSerializerDefaults.Web);

    private readonly IProductService _productService;
    private readonly ISegmentationService _segmentationService;

    public VisualizerController(IProductService productService, ISegmentationService segmentationService)
    {
        _productService = productService;
        _segmentationService = segmentationService;
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _productService.GetVisualizerProductsAsync();
        return Ok(products);
    }

    [HttpPost("segment")]
    [RequestSizeLimit(12_000_000)]
    public async Task<IActionResult> Segment(IFormFile photo, [FromForm] string points)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(new { error = "Снимката е задължителна." });

        var parsed = ParsePoints(points);
        if (parsed is null || parsed.Count == 0)
            return BadRequest(new { error = "Моля, докоснете областта, която искате да покриете." });

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await using var stream = photo.OpenReadStream();
        var outcome = await _segmentationService.SegmentNewAsync(stream, parsed, clientIp);
        return ToActionResult(outcome);
    }

    [HttpPost("segment/{sessionToken}")]
    public async Task<IActionResult> Refine(string sessionToken, [FromBody] List<SegmentPointDto> points)
    {
        if (points is null || points.Count == 0)
            return BadRequest(new { error = "Моля, докоснете областта, която искате да покриете." });

        var outcome = await _segmentationService.RefineAsync(sessionToken,
            points.Select(p => new SamPoint((float)p.X, (float)p.Y, p.Label)).ToList());
        return ToActionResult(outcome);
    }

    private static List<SamPoint>? ParsePoints(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var dtos = JsonSerializer.Deserialize<List<SegmentPointDto>>(json, PointsJson);
            return dtos?.Select(p => new SamPoint((float)p.X, (float)p.Y, p.Label)).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private IActionResult ToActionResult(SegmentOutcome outcome)
    {
        if (outcome.StatusCode != 200 || outcome.Result is null)
            return StatusCode(outcome.StatusCode, new { error = outcome.Error });

        return Ok(new SegmentResponse
        {
            SessionToken = outcome.Result.SessionToken,
            MaskPng = outcome.Result.MaskPng,
            Width = outcome.Result.Width,
            Height = outcome.Result.Height
        });
    }
}
```

- [ ] **Step 3: Wire DI and configuration**

In `src/NaturalStoneImpex.Api/Program.cs`, after the existing service registrations (`builder.Services.AddScoped<IInvoiceService, InvoiceService>();`) add:

```csharp
// Visualizer (see docs/visualizer-specification.md)
builder.Services.Configure<VisualizerOptions>(builder.Configuration.GetSection("Visualizer"));
builder.Services.AddMemoryCache(options => options.SizeLimit = 16); // embeddings are ~4 MB each
builder.Services.AddSingleton<EncodeGate>();
builder.Services.AddSingleton<ISamModel>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<VisualizerOptions>>().Value;
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    return new SamOnnxModel(
        Path.Combine(env.ContentRootPath, options.EncoderPath),
        Path.Combine(env.ContentRootPath, options.DecoderPath));
});
builder.Services.AddScoped<ISegmentationService, SegmentationService>();
```

and add `using NaturalStoneImpex.Api.Services.Segmentation;` to the usings.

In `src/NaturalStoneImpex.Api/appsettings.json` add a top-level section:

```json
  "Visualizer": {
    "Enabled": true,
    "EncoderPath": "MLModels/mobilesam-encoder.onnx",
    "DecoderPath": "MLModels/mobilesam-decoder.onnx",
    "MaxUploadBytes": 10485760,
    "MaxImageDimension": 2048,
    "MaxConcurrentEncodes": 2,
    "EmbeddingCacheMinutes": 15,
    "PerIpDailyLimit": 20,
    "GlobalDailyLimit": 500
  }
```

- [ ] **Step 4: Build and verify end-to-end with curl**

```powershell
dotnet build
dotnet run --project src/NaturalStoneImpex.Api
```

In a second terminal (any small JPG works as `photo.jpg`):

```powershell
curl.exe -k -X POST https://localhost:5001/api/visualizer/segment -F "photo=@photo.jpg" -F "points=[{\"x\":100,\"y\":100,\"label\":1}]"
```
Expected: `200` JSON with `sessionToken`, non-empty `maskPng`, `width`, `height` (needs models from Task 3 downloaded; otherwise expect `503 {"error":"Визуализаторът е временно недостъпен."}` — also a valid check).

```powershell
curl.exe -k -X POST https://localhost:5001/api/visualizer/segment/deadbeef -H "Content-Type: application/json" -d "[{\"x\":1,\"y\":1,\"label\":1}]"
```
Expected: `404 {"error":"Сесията е изтекла. Моля, качете снимката отново."}`.

- [ ] **Step 5: Run all tests**

Run: `dotnet test`
Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): segment endpoints with config and DI"
```

---

### Task 7: visualizer.js — homography + WebGL rendering core

**Files:**
- Create: `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`
- Create: `tests/manual/visualizer-harness.html`
- Modify: `src/NaturalStoneImpex.Client/wwwroot/index.html` (script tag before the blazor script)

**Interfaces:**
- Produces `window.nsiVisualizer` with (Tasks 8, 10, 11 depend on these exact names):
  - `init(stageElementId, dotNetRef|null, options|null)` → `{ webgl: boolean }` — builds `<img>` + overlay canvases inside the stage div; `options.forceFallback` for testing.
  - `loadPhotoFromDataUrl(dataUrl)` → Promise `{ width, height }`
  - `setMaskPng(base64Png)` → Promise (draws server mask into the internal mask canvas, rebuilds derived textures)
  - `clearMask()`, `hasMask()` → boolean, `setMaskVisible(bool)` (green tint overlay)
  - `defaultCornersFromMask()` → `[tlx,tly,trx,try,brx,bry,blx,bly]` (photo px), `setCorners(cornersArray)`
  - `setProductTexture(url, widthMeters)` → Promise, `setScale(factor)`, `setRotation(degrees)`
  - `render()`, `setCompareRatio(percent)` (0 = full "after", 100 = full "before")
  - `exportResultDataUrl()` → jpeg data URL, `downloadResult(filename)`
  - `dispose()`
  - Internal test hooks: `_test.computeHomography(src, dst)`, `_test.applyH(h, x, y)` → `[x', y']`
- The homography maps a virtual ground rectangle of 10 m × 15 m (constants `GROUND_W = 10`, `GROUND_H = 15`) onto the 4 corner points (order: top-left, top-right, bottom-right, bottom-left, in photo pixels). Texture tile physical size = `widthMeters × scaleFactor` meters.

- [ ] **Step 1: Write the failing test harness**

Create `tests/manual/visualizer-harness.html` (opened directly from disk with `file://`; everything is procedural so no CORS issues):

```html
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>visualizer.js harness</title>
<style>
  body { font-family: sans-serif; margin: 20px; }
  #stage { position: relative; width: 800px; }
  #status { font-weight: bold; font-size: 1.2em; }
  .pass { color: green; } .fail { color: red; }
  label { display: inline-block; width: 120px; }
</style>
</head>
<body>
<h1>visualizer.js harness</h1>
<p id="status">running…</p>
<div>
  <label>Scale</label><input id="scale" type="range" min="0.3" max="3" step="0.05" value="1">
  <label>Rotation</label><input id="rot" type="range" min="0" max="90" step="1" value="0">
  <label>Compare</label><input id="cmp" type="range" min="0" max="100" step="1" value="0">
</div>
<div id="stage"></div>
<script src="../../src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js"></script>
<script>
(async function () {
  const failures = [];
  const assert = (cond, msg) => { if (!cond) failures.push(msg); };
  const near = (a, b, eps) => Math.abs(a - b) < (eps || 1e-6);

  try {
    const viz = window.nsiVisualizer;
    assert(!!viz, "module loaded");

    // --- homography math ---
    // Identity: unit square -> unit square
    const sq = [0,0, 1,0, 1,1, 0,1];
    const hI = viz._test.computeHomography(sq, sq);
    const p = viz._test.applyH(hI, 0.5, 0.5);
    assert(near(p[0], 0.5) && near(p[1], 0.5), "identity homography");

    // Known projective map: square -> trapezoid; corners must map exactly
    const dst = [100,50, 700,80, 780,560, 40,540];
    const h = viz._test.computeHomography(sq, dst);
    const corners = [[0,0],[1,0],[1,1],[0,1]];
    corners.forEach((c, i) => {
      const q = viz._test.applyH(h, c[0], c[1]);
      assert(near(q[0], dst[i*2], 1e-3) && near(q[1], dst[i*2+1], 1e-3), "corner " + i + " maps exactly");
    });

    // --- rendering (visual) ---
    const forceFallback = new URLSearchParams(location.search).has("fallback");
    const mode = viz.init("stage", null, { forceFallback });
    document.title += forceFallback ? " (canvas-2d)" : (mode.webgl ? " (webgl)" : " (canvas-2d auto)");

    // Procedural photo: sky + green + gray trapezoid "driveway", 1200x900
    const photo = document.createElement("canvas");
    photo.width = 1200; photo.height = 900;
    const pctx = photo.getContext("2d");
    pctx.fillStyle = "#9ec3e6"; pctx.fillRect(0, 0, 1200, 380);
    pctx.fillStyle = "#5e9a4e"; pctx.fillRect(0, 380, 1200, 520);
    pctx.fillStyle = "#8a8a86";
    pctx.beginPath();
    pctx.moveTo(430, 400); pctx.lineTo(760, 400); pctx.lineTo(1050, 880); pctx.lineTo(180, 880);
    pctx.closePath(); pctx.fill();
    // add a dark "shadow" band to verify luminance transfer
    pctx.fillStyle = "rgba(20,20,30,0.45)";
    pctx.fillRect(150, 600, 1050, 120);
    await viz.loadPhotoFromDataUrl(photo.toDataURL("image/png"));

    // Procedural mask = same trapezoid, white on black
    const mask = document.createElement("canvas");
    mask.width = 1200; mask.height = 900;
    const mctx = mask.getContext("2d");
    mctx.fillStyle = "#000"; mctx.fillRect(0, 0, 1200, 900);
    mctx.fillStyle = "#fff";
    mctx.beginPath();
    mctx.moveTo(430, 400); mctx.lineTo(760, 400); mctx.lineTo(1050, 880); mctx.lineTo(180, 880);
    mctx.closePath(); mctx.fill();
    await viz.setMaskPng(mask.toDataURL("image/png").split(",")[1]);
    assert(viz.hasMask(), "mask registered");

    const def = viz.defaultCornersFromMask();
    assert(def.length === 8, "default corners has 8 values");
    assert(def[7] > def[1], "bottom corners below top corners");
    viz.setCorners(def);

    // Procedural stone texture
    const tile = document.createElement("canvas");
    tile.width = 256; tile.height = 256;
    const tctx = tile.getContext("2d");
    tctx.fillStyle = "#b8b0a0"; tctx.fillRect(0, 0, 256, 256);
    tctx.strokeStyle = "#6b6458"; tctx.lineWidth = 6;
    for (let i = 0; i <= 2; i++) {
      tctx.beginPath(); tctx.moveTo(0, i * 128); tctx.lineTo(256, i * 128); tctx.stroke();
      tctx.beginPath(); tctx.moveTo(i * 128, 0); tctx.lineTo(i * 128, 256); tctx.stroke();
    }
    await viz.setProductTexture(tile.toDataURL("image/png"), 1.0);
    viz.render();

    const dataUrl = viz.exportResultDataUrl();
    assert(dataUrl.startsWith("data:image/jpeg"), "export produces jpeg data url");

    document.getElementById("scale").oninput = e => { viz.setScale(parseFloat(e.target.value)); viz.render(); };
    document.getElementById("rot").oninput = e => { viz.setRotation(parseFloat(e.target.value)); viz.render(); };
    document.getElementById("cmp").oninput = e => viz.setCompareRatio(parseFloat(e.target.value));
  } catch (err) {
    failures.push("exception: " + err.message);
    console.error(err);
  }

  const status = document.getElementById("status");
  if (failures.length === 0) {
    status.textContent = "ALL PASS — now verify visually: stones must recede with perspective, shadow band must remain visible on the stones.";
    status.className = "pass";
  } else {
    status.textContent = "FAIL: " + failures.join("; ");
    status.className = "fail";
  }
})();
</script>
</body>
</html>
```

- [ ] **Step 2: Open harness to verify it fails**

Open `tests/manual/visualizer-harness.html` in Chrome (double-click).
Expected: red **FAIL: module loaded** (visualizer.js does not exist yet).

- [ ] **Step 3: Implement the module**

Create `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`:

```javascript
// Product visualizer rendering engine. Plain JS, driven from Blazor via JS interop.
// Pipeline: product texture -> homography warp (ground plane) -> mask clip -> luminance transfer.
// WebGL1 primary renderer (true projective mapping); canvas-2D fallback in Task 8.
window.nsiVisualizer = (function () {
  'use strict';

  var GROUND_W = 10;   // meters spanned by the perspective quad, left-right
  var GROUND_H = 15;   // meters spanned near-far (heuristic for a typical tilted photo)

  var stage = null, photoImg = null, glCanvas = null, editCanvas = null;
  var gl = null, program = null, uniforms = {};
  var dotNetRef = null, forceFallback = false;
  var photoW = 0, photoH = 0;
  var maskCanvas = null, blurredMask = null, maskPresent = false;
  var corners = null, groundToPx = null, pxToGround = null;
  var tileSource = null, tileMeters = 1.0, scaleFactor = 1.0, rotationRad = 0;
  var photoTexture = null, maskTexture = null, tileTexture = null, lumMean = 0.5;

  // ---------- linear algebra ----------

  // Solve the 8x8 system for a homography h (h9 = 1) mapping src[i] -> dst[i], 4 point pairs.
  // src/dst are flat arrays [x0,y0, x1,y1, x2,y2, x3,y3]. Returns row-major 9-element array.
  function computeHomography(src, dst) {
    var a = [], b = [];
    for (var i = 0; i < 4; i++) {
      var sx = src[i * 2], sy = src[i * 2 + 1];
      var dx = dst[i * 2], dy = dst[i * 2 + 1];
      a.push([sx, sy, 1, 0, 0, 0, -dx * sx, -dx * sy]); b.push(dx);
      a.push([0, 0, 0, sx, sy, 1, -dy * sx, -dy * sy]); b.push(dy);
    }
    // Gaussian elimination with partial pivoting
    for (var col = 0; col < 8; col++) {
      var pivot = col;
      for (var r = col + 1; r < 8; r++)
        if (Math.abs(a[r][col]) > Math.abs(a[pivot][col])) pivot = r;
      var tmp = a[col]; a[col] = a[pivot]; a[pivot] = tmp;
      var tb = b[col]; b[col] = b[pivot]; b[pivot] = tb;
      for (var row = col + 1; row < 8; row++) {
        var f = a[row][col] / a[col][col];
        for (var k = col; k < 8; k++) a[row][k] -= f * a[col][k];
        b[row] -= f * b[col];
      }
    }
    var h = new Array(8);
    for (var rr = 7; rr >= 0; rr--) {
      var sum = b[rr];
      for (var cc = rr + 1; cc < 8; cc++) sum -= a[rr][cc] * h[cc];
      h[rr] = sum / a[rr][rr];
    }
    return [h[0], h[1], h[2], h[3], h[4], h[5], h[6], h[7], 1];
  }

  function invert3(m) {
    var a = m[0], b = m[1], c = m[2], d = m[3], e = m[4], f = m[5], g = m[6], h = m[7], i = m[8];
    var A = e * i - f * h, B = c * h - b * i, C = b * f - c * e;
    var det = a * A + d * B + g * C;
    return [A / det, B / det, C / det,
            (f * g - d * i) / det, (a * i - c * g) / det, (c * d - a * f) / det,
            (d * h - e * g) / det, (b * g - a * h) / det, (a * e - b * d) / det];
  }

  function applyH(m, x, y) {
    var w = m[6] * x + m[7] * y + m[8];
    return [(m[0] * x + m[1] * y + m[2]) / w, (m[3] * x + m[4] * y + m[5]) / w];
  }

  // ---------- WebGL ----------

  var VS = 'attribute vec2 a_pos; varying vec2 v_uv;' +
    'void main(){ v_uv = a_pos * 0.5 + 0.5; gl_Position = vec4(a_pos, 0.0, 1.0); }';

  var FS = 'precision highp float; varying vec2 v_uv;' +
    'uniform vec2 u_size; uniform sampler2D u_photo; uniform sampler2D u_tile; uniform sampler2D u_mask;' +
    'uniform mat3 u_invH; uniform float u_tileMeters; uniform float u_rot; uniform float u_lumMean;' +
    'void main(){' +
    '  vec2 uv = vec2(v_uv.x, 1.0 - v_uv.y);' +           // top-left origin, matches image rows
    '  float m = texture2D(u_mask, uv).r;' +
    '  if (m < 0.01) { gl_FragColor = vec4(0.0); return; }' +
    '  vec2 px = uv * u_size;' +
    '  vec3 g = u_invH * vec3(px, 1.0);' +
    '  vec2 ground = g.xy / g.z;' +
    '  float c = cos(u_rot); float s = sin(u_rot);' +
    '  ground = mat2(c, -s, s, c) * ground;' +
    '  vec3 stone = texture2D(u_tile, ground / u_tileMeters).rgb;' +
    '  float lum = dot(texture2D(u_photo, uv).rgb, vec3(0.299, 0.587, 0.114));' +
    '  float shade = clamp(lum / max(u_lumMean, 0.05), 0.25, 1.6);' +
    '  gl_FragColor = vec4(stone * shade, m);' +
    '}';

  function compileShader(type, source) {
    var shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS))
      throw new Error('shader: ' + gl.getShaderInfoLog(shader));
    return shader;
  }

  function initGl() {
    gl = glCanvas.getContext('webgl', { alpha: true, preserveDrawingBuffer: true });
    if (!gl) return false;
    program = gl.createProgram();
    gl.attachShader(program, compileShader(gl.VERTEX_SHADER, VS));
    gl.attachShader(program, compileShader(gl.FRAGMENT_SHADER, FS));
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS))
      throw new Error('link: ' + gl.getProgramInfoLog(program));
    gl.useProgram(program);

    var quad = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, quad);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]), gl.STATIC_DRAW);
    var aPos = gl.getAttribLocation(program, 'a_pos');
    gl.enableVertexAttribArray(aPos);
    gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);

    ['u_size', 'u_photo', 'u_tile', 'u_mask', 'u_invH', 'u_tileMeters', 'u_rot', 'u_lumMean']
      .forEach(function (n) { uniforms[n] = gl.getUniformLocation(program, n); });
    gl.uniform1i(uniforms.u_photo, 0);
    gl.uniform1i(uniforms.u_tile, 1);
    gl.uniform1i(uniforms.u_mask, 2);
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    gl.clearColor(0, 0, 0, 0);
    return true;
  }

  function uploadTexture(existing, source, repeat) {
    var texture = existing || gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, source);
    if (repeat) { // requires power-of-two source
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.REPEAT);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.REPEAT);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
      gl.generateMipmap(gl.TEXTURE_2D);
    } else {
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    }
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    return texture;
  }

  // ---------- derived mask data ----------

  function rebuildMaskDerived() {
    blurredMask = document.createElement('canvas');
    blurredMask.width = photoW; blurredMask.height = photoH;
    var ctx = blurredMask.getContext('2d');
    ctx.filter = 'blur(3px)';                 // feathered edge
    ctx.drawImage(maskCanvas, 0, 0);

    // mean luminance of the photo inside the mask (for shadow-preserving shading)
    var w = 128, h = Math.max(1, Math.round(photoH / photoW * 128));
    var ps = document.createElement('canvas'); ps.width = w; ps.height = h;
    ps.getContext('2d').drawImage(photoImg, 0, 0, w, h);
    var ms = document.createElement('canvas'); ms.width = w; ms.height = h;
    ms.getContext('2d').drawImage(maskCanvas, 0, 0, w, h);
    var pd = ps.getContext('2d').getImageData(0, 0, w, h).data;
    var md = ms.getContext('2d').getImageData(0, 0, w, h).data;
    var sum = 0, count = 0;
    for (var i = 0; i < pd.length; i += 4) {
      if (md[i] > 128) {
        sum += (0.299 * pd[i] + 0.587 * pd[i + 1] + 0.114 * pd[i + 2]) / 255;
        count++;
      }
    }
    lumMean = count > 0 ? sum / count : 0.5;
    if (gl) maskTexture = uploadTexture(maskTexture, blurredMask, false);
    drawMaskTint();
  }

  function drawMaskTint() {
    if (!editCanvas) return;
    var ctx = editCanvas.getContext('2d');
    ctx.clearRect(0, 0, photoW, photoH);
    if (!maskPresent || editCanvas.style.display === 'none') return;
    ctx.drawImage(maskCanvas, 0, 0);
    ctx.globalCompositeOperation = 'source-in';
    ctx.fillStyle = 'rgba(40, 200, 90, 0.35)';
    ctx.fillRect(0, 0, photoW, photoH);
    ctx.globalCompositeOperation = 'source-over';
  }

  function maskBBox() {
    var w = 128, h = Math.max(1, Math.round(photoH / photoW * 128));
    var small = document.createElement('canvas'); small.width = w; small.height = h;
    small.getContext('2d').drawImage(maskCanvas, 0, 0, w, h);
    var d = small.getContext('2d').getImageData(0, 0, w, h).data;
    var minX = w, minY = h, maxX = 0, maxY = 0, found = false;
    for (var y = 0; y < h; y++)
      for (var x = 0; x < w; x++)
        if (d[(y * w + x) * 4] > 128) {
          found = true;
          if (x < minX) minX = x; if (x > maxX) maxX = x;
          if (y < minY) minY = y; if (y > maxY) maxY = y;
        }
    if (!found) return { x0: 0.1 * photoW, y0: 0.4 * photoH, x1: 0.9 * photoW, y1: 0.95 * photoH };
    var sx = photoW / w, sy = photoH / h;
    return { x0: minX * sx, y0: minY * sy, x1: (maxX + 1) * sx, y1: (maxY + 1) * sy };
  }

  // ---------- public API ----------

  var api = {
    init: function (stageId, ref, options) {
      stage = document.getElementById(stageId);
      dotNetRef = ref || null;
      forceFallback = !!(options && options.forceFallback);
      stage.style.position = 'relative';
      stage.innerHTML = '';

      photoImg = document.createElement('img');
      photoImg.style.cssText = 'display:block;width:100%;height:auto;user-select:none;-webkit-user-drag:none;';
      glCanvas = document.createElement('canvas');
      glCanvas.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;pointer-events:none;';
      editCanvas = document.createElement('canvas');
      editCanvas.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;touch-action:none;';
      stage.appendChild(photoImg);
      stage.appendChild(glCanvas);
      stage.appendChild(editCanvas);

      var webgl = false;
      if (!forceFallback) {
        try { webgl = initGl(); } catch (e) { console.warn('WebGL unavailable:', e); webgl = false; }
      }
      if (!webgl) gl = null;
      if (api._wireEvents) api._wireEvents(); // installed by the interaction layer (Task 8)
      return { webgl: !!gl };
    },

    loadPhotoFromDataUrl: function (dataUrl) {
      return new Promise(function (resolve, reject) {
        photoImg.onload = function () {
          photoW = photoImg.naturalWidth; photoH = photoImg.naturalHeight;
          glCanvas.width = photoW; glCanvas.height = photoH;
          editCanvas.width = photoW; editCanvas.height = photoH;
          maskCanvas = document.createElement('canvas');
          maskCanvas.width = photoW; maskCanvas.height = photoH;
          maskCanvas.getContext('2d').fillStyle = '#000';
          maskCanvas.getContext('2d').fillRect(0, 0, photoW, photoH);
          maskPresent = false;
          if (gl) {
            gl.viewport(0, 0, photoW, photoH);
            photoTexture = uploadTexture(photoTexture, photoImg, false);
            gl.clear(gl.COLOR_BUFFER_BIT);
          }
          resolve({ width: photoW, height: photoH });
        };
        photoImg.onerror = reject;
        photoImg.crossOrigin = 'anonymous';
        photoImg.src = dataUrl;
      });
    },

    setMaskPng: function (base64) {
      return new Promise(function (resolve, reject) {
        var img = new Image();
        img.onload = function () {
          var ctx = maskCanvas.getContext('2d');
          ctx.fillStyle = '#000';
          ctx.fillRect(0, 0, photoW, photoH);
          ctx.drawImage(img, 0, 0, photoW, photoH);
          maskPresent = true;
          rebuildMaskDerived();
          resolve();
        };
        img.onerror = reject;
        img.src = 'data:image/png;base64,' + base64;
      });
    },

    clearMask: function () {
      var ctx = maskCanvas.getContext('2d');
      ctx.fillStyle = '#000';
      ctx.fillRect(0, 0, photoW, photoH);
      maskPresent = false;
      rebuildMaskDerived();
      if (gl) gl.clear(gl.COLOR_BUFFER_BIT);
    },

    hasMask: function () { return maskPresent; },

    setMaskVisible: function (visible) {
      editCanvas.style.display = visible ? 'block' : 'none';
      drawMaskTint();
    },

    defaultCornersFromMask: function () {
      var box = maskBBox();
      var cx = (box.x0 + box.x1) / 2;
      var halfTop = (box.x1 - box.x0) * 0.45 / 2; // spec: top edge ~45% of bottom width
      return [cx - halfTop, box.y0, cx + halfTop, box.y0, box.x1, box.y1, box.x0, box.y1];
    },

    setCorners: function (c) {
      corners = c.slice();
      var srcGround = [0, 0, GROUND_W, 0, GROUND_W, GROUND_H, 0, GROUND_H];
      groundToPx = computeHomography(srcGround, corners);
      pxToGround = invert3(groundToPx);
    },

    setProductTexture: function (url, widthMeters) {
      tileMeters = widthMeters || 1.0;
      return new Promise(function (resolve, reject) {
        var img = new Image();
        img.crossOrigin = 'anonymous';
        img.onload = function () {
          // Resize to power-of-two so WebGL REPEAT + mipmaps work.
          var pot = document.createElement('canvas');
          pot.width = 1024; pot.height = 1024;
          pot.getContext('2d').drawImage(img, 0, 0, 1024, 1024);
          tileSource = pot;
          if (gl) tileTexture = uploadTexture(tileTexture, pot, true);
          resolve();
        };
        img.onerror = reject;
        img.src = url;
      });
    },

    setScale: function (f) { scaleFactor = f; },
    setRotation: function (deg) { rotationRad = deg * Math.PI / 180; },

    render: function () {
      if (!maskPresent || !tileSource || !pxToGround) return;
      if (!gl) { api._renderFallback(); return; } // Task 8
      gl.clear(gl.COLOR_BUFFER_BIT);
      gl.uniform2f(uniforms.u_size, photoW, photoH);
      // row-major -> column-major for uniformMatrix3fv
      var m = pxToGround;
      gl.uniformMatrix3fv(uniforms.u_invH, false,
        [m[0], m[3], m[6], m[1], m[4], m[7], m[2], m[5], m[8]]);
      gl.uniform1f(uniforms.u_tileMeters, tileMeters * scaleFactor);
      gl.uniform1f(uniforms.u_rot, rotationRad);
      gl.uniform1f(uniforms.u_lumMean, lumMean);
      gl.activeTexture(gl.TEXTURE0); gl.bindTexture(gl.TEXTURE_2D, photoTexture);
      gl.activeTexture(gl.TEXTURE1); gl.bindTexture(gl.TEXTURE_2D, tileTexture);
      gl.activeTexture(gl.TEXTURE2); gl.bindTexture(gl.TEXTURE_2D, maskTexture);
      gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    },

    setCompareRatio: function (percent) {
      glCanvas.style.clipPath = 'inset(0 0 0 ' + percent + '%)';
    },

    exportResultDataUrl: function () {
      var out = document.createElement('canvas');
      out.width = photoW; out.height = photoH;
      var ctx = out.getContext('2d');
      ctx.drawImage(photoImg, 0, 0, photoW, photoH);
      ctx.drawImage(glCanvas, 0, 0, photoW, photoH);
      return out.toDataURL('image/jpeg', 0.92);
    },

    downloadResult: function (filename) {
      var link = document.createElement('a');
      link.download = filename;
      link.href = api.exportResultDataUrl();
      link.click();
    },

    dispose: function () {
      dotNetRef = null;
      if (stage) stage.innerHTML = '';
      gl = null; photoTexture = null; maskTexture = null; tileTexture = null;
    },

    _test: { computeHomography: computeHomography, applyH: applyH, invert3: invert3 },
    _internal: function () {
      return {
        get maskCanvas() { return maskCanvas; },
        get photoImg() { return photoImg; },
        get glCanvas() { return glCanvas; },
        get editCanvas() { return editCanvas; },
        get groundToPx() { return groundToPx; },
        get dotNetRef() { return dotNetRef; },
        get size() { return { w: photoW, h: photoH }; },
        get tile() { return { source: tileSource, meters: tileMeters, scale: scaleFactor, rot: rotationRad }; },
        get lumMean() { return lumMean; },
        get blurredMask() { return blurredMask; },
        setMaskPresent: function (v) { maskPresent = v; },
        rebuildMaskDerived: rebuildMaskDerived,
        GROUND_W: GROUND_W, GROUND_H: GROUND_H
      };
    }
  };

  return api;
})();
```

- [ ] **Step 4: Open harness to verify it passes**

Open `tests/manual/visualizer-harness.html` in Chrome.
Expected: green **ALL PASS** plus a rendered image where the gray trapezoid is covered with the grid-stone texture, the pattern gets smaller toward the top (perspective), and the dark shadow band is visible **on** the stones (luminance transfer). Move the three sliders: scale/rotation re-render correctly; compare reveals the original from the left.

- [ ] **Step 5: Register the script in the Blazor client**

In `src/NaturalStoneImpex.Client/wwwroot/index.html`, before the `<script src="_framework/blazor.webassembly.js"></script>` line add:

```html
    <script src="js/visualizer.js"></script>
```

Run: `dotnet build`
Expected: success.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): WebGL rendering engine with homography and luminance transfer"
```

---

### Task 8: visualizer.js — taps, brush editing, canvas-2D fallback

**Files:**
- Modify: `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`
- Modify: `tests/manual/visualizer-harness.html`

**Interfaces:**
- Consumes: `_internal()` accessors from Task 7.
- Produces (Tasks 10–11 depend on these):
  - `setMode(mode)` — `'tap-add' | 'tap-remove' | 'brush' | 'erase' | null`
  - `setBrushSize(px)` — brush diameter in photo pixels (default 40)
  - Tap modes invoke `dotNetRef.invokeMethodAsync('OnCanvasTapAsync', x, y, label)` with photo-pixel coordinates (label 1 for `tap-add`, 0 for `tap-remove`).
  - Brush/erase strokes edit the mask locally; on stroke end the module rebuilds derived data, re-renders, and invokes `dotNetRef.invokeMethodAsync('OnMaskEditedAsync')`.
  - `api._renderFallback()` — canvas-2D renderer used automatically when WebGL is unavailable (or `forceFallback`).

- [ ] **Step 1: Extend the harness with interaction checks**

In `tests/manual/visualizer-harness.html`, inside the async function after the `exportResultDataUrl` assertion, add:

```javascript
    // --- interaction layer ---
    assert(typeof viz.setMode === "function", "setMode exists");
    assert(typeof viz.setBrushSize === "function", "setBrushSize exists");
    // brush stroke programmatically: paint a square patch and verify the mask grew
    const internals = viz._internal();
    const before = internals.maskCanvas.getContext("2d")
      .getImageData(60, 420, 1, 1).data[0];
    viz.setMode("brush");
    viz.setBrushSize(60);
    viz._test.strokeForTest(60, 420);
    const after = internals.maskCanvas.getContext("2d")
      .getImageData(60, 420, 1, 1).data[0];
    assert(before < 128 && after > 128, "brush paints the mask");
    viz.setMode(null);
```

Reload the harness. Expected: red **FAIL: setMode exists** (not implemented yet).

- [ ] **Step 2: Implement interaction + fallback renderer**

In `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`, add inside the module (before `return api;`) — a self-contained extension that uses `api._internal()`:

```javascript
  // ---------- interaction layer ----------

  var mode = null, brushSize = 40, stroking = false, strokeMoved = false;

  function eventToPhotoPx(evt) {
    var rect = photoImg.getBoundingClientRect();
    return {
      x: (evt.clientX - rect.left) / rect.width * photoW,
      y: (evt.clientY - rect.top) / rect.height * photoH
    };
  }

  function paintAt(x, y) {
    var ctx = maskCanvas.getContext('2d');
    ctx.globalCompositeOperation = mode === 'erase' ? 'destination-out' : 'source-over';
    ctx.fillStyle = '#fff';
    ctx.beginPath();
    ctx.arc(x, y, brushSize / 2, 0, Math.PI * 2);
    ctx.fill();
    ctx.globalCompositeOperation = 'source-over';
    maskPresent = true;
    drawMaskTint();
  }

  function onPointerDown(evt) {
    if (!mode || !photoW) return;
    evt.preventDefault();
    if (mode === 'brush' || mode === 'erase') {
      stroking = true;
      editCanvas.setPointerCapture(evt.pointerId);
      var p = eventToPhotoPx(evt);
      paintAt(p.x, p.y);
    }
    strokeMoved = false;
  }

  function onPointerMove(evt) {
    if (!stroking) return;
    strokeMoved = true;
    var p = eventToPhotoPx(evt);
    paintAt(p.x, p.y);
  }

  function onPointerUp(evt) {
    if (!mode || !photoW) return;
    var p = eventToPhotoPx(evt);
    if (mode === 'tap-add' || mode === 'tap-remove') {
      if (dotNetRef)
        dotNetRef.invokeMethodAsync('OnCanvasTapAsync', p.x, p.y, mode === 'tap-add' ? 1 : 0);
    } else if (stroking) {
      stroking = false;
      rebuildMaskDerived();
      api.render();
      if (dotNetRef) dotNetRef.invokeMethodAsync('OnMaskEditedAsync');
    }
  }

  api.setMode = function (m) {
    mode = m;
    editCanvas.style.cursor = (m === 'brush' || m === 'erase') ? 'crosshair'
      : (m ? 'pointer' : 'default');
  };
  api.setBrushSize = function (px) { brushSize = px; };
  api._wireEvents = function () {
    editCanvas.addEventListener('pointerdown', onPointerDown);
    editCanvas.addEventListener('pointermove', onPointerMove);
    editCanvas.addEventListener('pointerup', onPointerUp);
  };
  api._test.strokeForTest = function (x, y) { // deterministic brush for the harness
    paintAt(x, y);
    rebuildMaskDerived();
  };

  // ---------- canvas-2D fallback renderer ----------

  // Affine-draw img triangle (sx, sy)[3] onto ctx triangle (dx, dy)[3].
  function drawTriangle(ctx, img, s, d) {
    ctx.save();
    ctx.beginPath();
    ctx.moveTo(d[0], d[1]); ctx.lineTo(d[2], d[3]); ctx.lineTo(d[4], d[5]);
    ctx.closePath();
    ctx.clip();
    var denom = s[0] * (s[5] - s[3]) - s[2] * s[5] + s[4] * s[3] + (s[2] - s[4]) * s[1];
    var m11 = -(s[1] * (d[4] - d[2]) - s[3] * d[4] + s[5] * d[2] + (s[3] - s[5]) * d[0]) / denom;
    var m12 = (s[3] * d[5] + s[1] * (d[3] - d[5]) - s[5] * d[3] + (s[5] - s[3]) * d[1]) / denom;
    var m21 = (s[0] * (d[4] - d[2]) - s[2] * d[4] + s[4] * d[2] + (s[2] - s[4]) * d[0]) / denom;
    var m22 = -(s[2] * d[5] + s[0] * (d[3] - d[5]) - s[4] * d[3] + (s[4] - s[2]) * d[1]) / denom;
    var dx = (s[0] * (s[5] * d[2] - s[3] * d[4]) + s[1] * (s[2] * d[4] - s[4] * d[2]) + (s[3] * s[4] - s[2] * s[5]) * d[0]) / denom;
    var dy = (s[0] * (s[5] * d[3] - s[3] * d[5]) + s[1] * (s[2] * d[5] - s[4] * d[3]) + (s[3] * s[4] - s[2] * s[5]) * d[1]) / denom;
    ctx.transform(m11, m12, m21, m22, dx, dy);
    ctx.drawImage(img, 0, 0);
    ctx.restore();
  }

  api._renderFallback = function () {
    var ctx = glCanvas.getContext('2d');
    if (!ctx) return;
    ctx.clearRect(0, 0, photoW, photoH);

    // Big ground-space texture canvas: whole quad area, tiled + rotated pattern.
    var metersPerTile = tileMeters * scaleFactor;
    var ppm = Math.min(1024 / metersPerTile, 2048 / GROUND_W); // cap resolution
    var big = document.createElement('canvas');
    big.width = Math.round(GROUND_W * ppm);
    big.height = Math.round(GROUND_H * ppm);
    var bctx = big.getContext('2d');
    var tilePx = Math.max(8, Math.round(metersPerTile * ppm));
    var tileScaled = document.createElement('canvas');
    tileScaled.width = tilePx; tileScaled.height = tilePx;
    tileScaled.getContext('2d').drawImage(tileSource, 0, 0, tilePx, tilePx);
    bctx.save();
    bctx.translate(big.width / 2, big.height / 2);
    bctx.rotate(rotationRad);
    bctx.fillStyle = bctx.createPattern(tileScaled, 'repeat');
    var diag = Math.hypot(big.width, big.height);
    bctx.fillRect(-diag, -diag, diag * 2, diag * 2);
    bctx.restore();

    // Warp big canvas onto the photo through the homography, cell by cell (2 triangles each).
    var pavedLayer = document.createElement('canvas');
    pavedLayer.width = photoW; pavedLayer.height = photoH;
    var pctx = pavedLayer.getContext('2d');
    var cells = 12;
    for (var gy = 0; gy < cells; gy++) {
      for (var gx = 0; gx < cells; gx++) {
        var gx0 = gx / cells * GROUND_W, gx1 = (gx + 1) / cells * GROUND_W;
        var gy0 = gy / cells * GROUND_H, gy1 = (gy + 1) / cells * GROUND_H;
        var p00 = applyH(groundToPx, gx0, gy0), p10 = applyH(groundToPx, gx1, gy0);
        var p11 = applyH(groundToPx, gx1, gy1), p01 = applyH(groundToPx, gx0, gy1);
        var sx0 = gx0 * ppm, sx1 = gx1 * ppm, sy0 = gy0 * ppm, sy1 = gy1 * ppm;
        drawTriangle(pctx, big, [sx0, sy0, sx1, sy0, sx1, sy1],
          [p00[0], p00[1], p10[0], p10[1], p11[0], p11[1]]);
        drawTriangle(pctx, big, [sx0, sy0, sx1, sy1, sx0, sy1],
          [p00[0], p00[1], p11[0], p11[1], p01[0], p01[1]]);
      }
    }

    // Luminance transfer (approximate): multiply by brightened grayscale photo.
    pctx.globalCompositeOperation = 'multiply';
    pctx.filter = 'grayscale(1) brightness(' + (1 / Math.max(lumMean, 0.2)).toFixed(2) + ')';
    pctx.drawImage(photoImg, 0, 0, photoW, photoH);
    pctx.filter = 'none';
    // Clip to the (feathered) mask.
    pctx.globalCompositeOperation = 'destination-in';
    pctx.drawImage(blurredMask, 0, 0);
    pctx.globalCompositeOperation = 'source-over';

    ctx.drawImage(pavedLayer, 0, 0);
  };
```

Note: `mode`, `brushSize` etc. live in the same closure, so the Task 7 variables (`photoImg`, `maskCanvas`, `editCanvas`, `photoW`, `photoH`, `maskPresent`, `drawMaskTint`, `rebuildMaskDerived`, `groundToPx`, `tileSource`, `tileMeters`, `scaleFactor`, `rotationRad`, `lumMean`, `blurredMask`, `dotNetRef`) are directly accessible — place this code inside the IIFE, after the `api` object is defined and before `return api;`. Remove `api._internal` usage in the harness if you prefer, but keep `_internal` itself (it is used by the harness assertions).

- [ ] **Step 3: Verify both render paths in the harness**

1. Open `tests/manual/visualizer-harness.html` → Expected: green **ALL PASS** (WebGL path + brush assertion).
2. Open `tests/manual/visualizer-harness.html?fallback=1` → Expected: green **ALL PASS**, title suffix `(canvas-2d)`, and a visually similar (slightly softer) rendering.

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): mask editing tools and canvas-2d fallback renderer"
```

---

### Task 9: Client models + VisualizerService

**Files:**
- Create: `src/NaturalStoneImpex.Client/Models/VisualizerProductDto.cs`
- Create: `src/NaturalStoneImpex.Client/Models/SegmentPoint.cs`
- Create: `src/NaturalStoneImpex.Client/Models/SegmentResponse.cs`
- Modify: `src/NaturalStoneImpex.Client/Models/ProductDto.cs`
- Modify: `src/NaturalStoneImpex.Client/Models/CreateProductRequest.cs`
- Modify: `src/NaturalStoneImpex.Client/Models/UpdateProductRequest.cs`
- Create: `src/NaturalStoneImpex.Client/Services/IVisualizerService.cs`
- Create: `src/NaturalStoneImpex.Client/Services/VisualizerService.cs`
- Modify: `src/NaturalStoneImpex.Client/Program.cs`

**Interfaces:**
- Consumes: HTTP contract from Tasks 2 and 6.
- Produces (Tasks 10–13 depend on these):

```csharp
public interface IVisualizerService
{
    Task<List<VisualizerProductDto>> GetProductsAsync();                       // paths resolved to absolute URLs
    Task<(SegmentResponse? Result, string? Error)> SegmentAsync(byte[] photoBytes, List<SegmentPoint> points);
    Task<(SegmentResponse? Result, string? Error, bool SessionExpired)> RefineAsync(string sessionToken, List<SegmentPoint> points);
}
```

- [ ] **Step 1: Create the models**

Create `src/NaturalStoneImpex.Client/Models/VisualizerProductDto.cs`:

```csharp
namespace NaturalStoneImpex.Client.Models;

public record VisualizerProductDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ImagePath { get; set; }          // set: resolved to absolute URL client-side
    public string TexturePath { get; set; } = string.Empty;
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

Create `src/NaturalStoneImpex.Client/Models/SegmentPoint.cs`:

```csharp
namespace NaturalStoneImpex.Client.Models;

/// <summary>Tap point in photo pixel coordinates. Label: 1 = add area, 0 = remove area.</summary>
public record SegmentPoint(double X, double Y, int Label);
```

Create `src/NaturalStoneImpex.Client/Models/SegmentResponse.cs`:

```csharp
namespace NaturalStoneImpex.Client.Models;

public record SegmentResponse
{
    public string SessionToken { get; init; } = string.Empty;
    public string MaskPng { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
}
```

In `src/NaturalStoneImpex.Client/Models/ProductDto.cs` add after `IsActive` (match the API ProductDto from Task 1):

```csharp
    public bool IsVisualizerEnabled { get; init; }
    public string? TextureImagePath { get; set; }
    public decimal TextureWidthMeters { get; init; }
```

In **both** client `CreateProductRequest.cs` and `UpdateProductRequest.cs` add after `StockQuantity` (mirror the API requests from Task 1 — check the client files' property style and keep it):

```csharp
    public bool IsVisualizerEnabled { get; set; }
    public decimal TextureWidthMeters { get; set; } = 1.00m;
```

- [ ] **Step 2: Create the service**

Create `src/NaturalStoneImpex.Client/Services/IVisualizerService.cs`:

```csharp
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public interface IVisualizerService
{
    Task<List<VisualizerProductDto>> GetProductsAsync();
    Task<(SegmentResponse? Result, string? Error)> SegmentAsync(byte[] photoBytes, List<SegmentPoint> points);
    Task<(SegmentResponse? Result, string? Error, bool SessionExpired)> RefineAsync(string sessionToken, List<SegmentPoint> points);
}
```

Create `src/NaturalStoneImpex.Client/Services/VisualizerService.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public class VisualizerService : IVisualizerService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public VisualizerService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiBaseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "";
    }

    public async Task<List<VisualizerProductDto>> GetProductsAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<VisualizerProductDto>>("api/visualizer/products");
        if (result is null) return new List<VisualizerProductDto>();
        foreach (var product in result)
        {
            product.ImagePath = Resolve(product.ImagePath);
            product.TexturePath = Resolve(product.TexturePath)!;
        }
        return result;
    }

    public async Task<(SegmentResponse? Result, string? Error)> SegmentAsync(byte[] photoBytes, List<SegmentPoint> points)
    {
        using var content = new MultipartFormDataContent();
        var photoContent = new ByteArrayContent(photoBytes);
        photoContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(photoContent, "photo", "photo.jpg");
        content.Add(new StringContent(JsonSerializer.Serialize(points, Json)), "points");

        try
        {
            var response = await _httpClient.PostAsync("api/visualizer/segment", content);
            if (!response.IsSuccessStatusCode)
                return (null, await ExtractErrorAsync(response));
            return (await response.Content.ReadFromJsonAsync<SegmentResponse>(), null);
        }
        catch (HttpRequestException)
        {
            return (null, "Възникна грешка при връзката със сървъра. Моля, опитайте отново.");
        }
    }

    public async Task<(SegmentResponse? Result, string? Error, bool SessionExpired)> RefineAsync(
        string sessionToken, List<SegmentPoint> points)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/visualizer/segment/{sessionToken}", points, Json);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return (null, null, true);
            if (!response.IsSuccessStatusCode)
                return (null, await ExtractErrorAsync(response), false);
            return (await response.Content.ReadFromJsonAsync<SegmentResponse>(), null, false);
        }
        catch (HttpRequestException)
        {
            return (null, "Възникна грешка при връзката със сървъра. Моля, опитайте отново.", false);
        }
    }

    private string? Resolve(string? path) =>
        string.IsNullOrEmpty(path) ? path : $"{_apiBaseUrl}{path}";

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        try
        {
            var errorDoc = JsonDocument.Parse(content);
            if (errorDoc.RootElement.TryGetProperty("error", out var errorMessage))
                return errorMessage.GetString() ?? "Възникна неочаквана грешка.";
        }
        catch (JsonException)
        {
        }
        return "Възникна неочаквана грешка.";
    }
}
```

- [ ] **Step 3: Register and build**

In `src/NaturalStoneImpex.Client/Program.cs`, after `builder.Services.AddScoped<IInvoiceService, InvoiceService>();` add:

```csharp
builder.Services.AddScoped<IVisualizerService, VisualizerService>();
```

Run: `dotnet build`
Expected: success.

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): client models and API service"
```

---

### Task 10: Visualizer page — upload, tap-to-segment, render (happy path)

**Files:**
- Create: `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`
- Modify: `src/NaturalStoneImpex.Client/wwwroot/css/app.css` (append; if the stylesheet has a different name, use the one `index.html` references)

**Interfaces:**
- Consumes: `IVisualizerService` (Task 9), `window.nsiVisualizer` API (Tasks 7–8).
- Produces: `/visualizer` route; `[JSInvokable] OnCanvasTapAsync(double x, double y, int label)` and `[JSInvokable] OnMaskEditedAsync()` (names must match the JS calls from Task 8). Accepts query string `?productId=N` (used by Task 12). Task 11 extends this file.

- [ ] **Step 1: Create the page**

Create `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`:

```razor
@page "/visualizer"
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.WebUtilities
@using NaturalStoneImpex.Client.Models
@using NaturalStoneImpex.Client.Services
@inject IVisualizerService VisualizerService
@inject NavigationManager Navigation
@inject IJSRuntime JS
@implements IAsyncDisposable

<PageTitle>Визуализатор — Natural Stone Impex</PageTitle>

<h1 class="mb-2">Визуализатор</h1>
<p class="text-muted">Качете снимка на вашия двор или алея и вижте как ще изглежда с нашите настилки.</p>

@if (_products is null)
{
    <div class="text-center my-5">
        <div class="spinner-border" role="status"><span class="visually-hidden">Зареждане…</span></div>
    </div>
}
else if (_products.Count == 0)
{
    <div class="alert alert-info">Визуализаторът не е наличен в момента.</div>
}
else if (!_photoLoaded)
{
    <div class="card mx-auto" style="max-width: 640px;">
        <div class="card-body">
            <h5 class="card-title">Качване на снимка</h5>
            <p class="card-text">
                Снимайте площта така, че да се вижда цялата повърхност, която искате да покриете.
                Избягвайте хора в кадъра.
            </p>
            <div class="form-check mb-3">
                <input class="form-check-input" type="checkbox" id="viz-consent" @bind="_consent">
                <label class="form-check-label" for="viz-consent">
                    Съгласен/на съм снимката да бъде обработена на сървъра на магазина за целите на
                    визуализацията. Снимката се изтрива автоматично след обработката.
                </label>
            </div>
            <InputFile OnChange="OnPhotoSelectedAsync" accept="image/*" capture="environment"
                       class="form-control" disabled="@(!_consent || _busy)" />
            @if (_error is not null)
            {
                <div class="alert alert-danger mt-3 mb-0">@_error</div>
            }
        </div>
    </div>
}
else
{
    <div class="mb-2">
        @if (!_hasMask)
        {
            <div class="alert alert-primary py-2">Докоснете областта, която искате да покриете с настилка.</div>
        }
    </div>

    <div class="position-relative">
        <div id="viz-stage"></div>
        @if (_busy)
        {
            <div class="viz-overlay d-flex align-items-center justify-content-center">
                <div class="text-center text-white">
                    <div class="spinner-border mb-2" role="status"></div>
                    <div>Разпознаваме областта…</div>
                </div>
            </div>
        }
    </div>

    <p class="text-muted small mt-2 mb-1">
        Визуализацията е ориентировъчна. Реалният продукт може да се различава по цвят и вид,
        а размерите са приблизителни.
    </p>

    @if (_error is not null)
    {
        <div class="alert alert-danger mt-2">@_error</div>
    }

    <button class="btn btn-outline-secondary mt-2" @onclick="ResetAsync" disabled="@_busy">Нова снимка</button>
}

@code {
    private List<VisualizerProductDto>? _products;
    private VisualizerProductDto? _selected;
    private bool _consent;
    private bool _photoLoaded;
    private byte[]? _photoBytes;
    private string? _sessionToken;
    private readonly List<SegmentPoint> _points = new();
    private bool _hasMask;
    private bool _busy;
    private string? _error;
    private bool _stageInitialized;
    private DotNetObjectReference<Visualizer>? _selfRef;

    protected override async Task OnInitializedAsync()
    {
        _products = await VisualizerService.GetProductsAsync();

        var query = QueryHelpers.ParseQuery(new Uri(Navigation.Uri).Query);
        if (query.TryGetValue("productId", out var idValue) && int.TryParse(idValue, out var id))
            _selected = _products.FirstOrDefault(p => p.Id == id);
        _selected ??= _products.FirstOrDefault();
    }

    private async Task OnPhotoSelectedAsync(InputFileChangeEventArgs e)
    {
        _error = null;
        _busy = true;
        try
        {
            // Downscale client-side: mobile photos are 8-12 MP; the server needs at most 2048 px.
            var resized = await e.File.RequestImageFileAsync("image/jpeg", 2048, 2048);
            await using var stream = resized.OpenReadStream(maxAllowedSize: 15 * 1024 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            _photoBytes = ms.ToArray();
            _photoLoaded = true;
            StateHasChanged(); // render the stage div before JS init
            await InitStageAsync();
        }
        catch (Exception)
        {
            _error = "Моля, качете снимка във формат JPG или PNG до 10 MB.";
            _photoLoaded = false;
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task InitStageAsync()
    {
        _selfRef ??= DotNetObjectReference.Create(this);
        await JS.InvokeAsync<object>("nsiVisualizer.init", "viz-stage", _selfRef, (object?)null);
        _stageInitialized = true;
        var dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(_photoBytes!);
        await JS.InvokeAsync<object>("nsiVisualizer.loadPhotoFromDataUrl", dataUrl);
        await JS.InvokeVoidAsync("nsiVisualizer.setMode", "tap-add");
        await JS.InvokeVoidAsync("nsiVisualizer.setMaskVisible", true);
    }

    [JSInvokable]
    public async Task OnCanvasTapAsync(double x, double y, int label)
    {
        if (_busy || _photoBytes is null) return;
        _busy = true;
        _error = null;
        StateHasChanged();
        try
        {
            _points.Add(new SegmentPoint(x, y, label));
            SegmentResponse? result;
            string? error;

            if (_sessionToken is null)
            {
                (result, error) = await VisualizerService.SegmentAsync(_photoBytes, _points);
            }
            else
            {
                bool expired;
                (result, error, expired) = await VisualizerService.RefineAsync(_sessionToken, _points);
                if (expired)
                {
                    // Embedding cache expired — transparently re-upload the kept photo bytes.
                    (result, error) = await VisualizerService.SegmentAsync(_photoBytes, _points);
                }
            }

            if (result is null)
            {
                _points.RemoveAt(_points.Count - 1);
                _error = error ?? "Областта не можа да бъде разпозната автоматично. Можете да я маркирате ръчно с четката.";
                return;
            }

            _sessionToken = result.SessionToken;
            await JS.InvokeAsync<object>("nsiVisualizer.setMaskPng", result.MaskPng);

            if (!_hasMask)
            {
                _hasMask = true;
                var corners = await JS.InvokeAsync<double[]>("nsiVisualizer.defaultCornersFromMask");
                await JS.InvokeVoidAsync("nsiVisualizer.setCorners", (object)corners);
            }

            await ApplySelectedProductAsync();
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }

    [JSInvokable]
    public Task OnMaskEditedAsync()
    {
        _hasMask = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task ApplySelectedProductAsync()
    {
        if (_selected is null || !_hasMask) return;
        await JS.InvokeAsync<object>("nsiVisualizer.setProductTexture",
            _selected.TexturePath, (double)_selected.TextureWidthMeters);
        await JS.InvokeVoidAsync("nsiVisualizer.render");
    }

    private async Task ResetAsync()
    {
        if (_stageInitialized)
            await JS.InvokeVoidAsync("nsiVisualizer.dispose");
        _stageInitialized = false;
        _photoLoaded = false;
        _photoBytes = null;
        _sessionToken = null;
        _points.Clear();
        _hasMask = false;
        _error = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_stageInitialized)
        {
            try { await JS.InvokeVoidAsync("nsiVisualizer.dispose"); }
            catch (JSDisconnectedException) { }
        }
        _selfRef?.Dispose();
    }
}
```

- [ ] **Step 2: Append the overlay style**

Append to the site stylesheet referenced by `index.html` (normally `src/NaturalStoneImpex.Client/wwwroot/css/app.css`):

```css
/* Product visualizer */
.viz-overlay {
    position: absolute;
    inset: 0;
    background: rgba(0, 0, 0, 0.45);
    z-index: 10;
}
```

- [ ] **Step 3: Build and verify the happy path manually**

```powershell
dotnet build
dotnet run --project src/NaturalStoneImpex.Api        # terminal 1 (models downloaded)
dotnet run --project src/NaturalStoneImpex.Client     # terminal 2
```

Prerequisite data: log into `/admin`, edit one product — upload a texture (any stone photo), set «Реална ширина» ≈ 1, enable it for the visualizer (admin UI fields arrive in Task 13 — until then set `IsVisualizerEnabled = 1` and `TextureImagePath` directly in the DB, or via Swagger `PUT /api/products/{id}` + `POST /api/products/{id}/texture`).

Open `https://localhost:5002/visualizer` and verify: consent gate works; photo uploads; tapping the ground shows the busy overlay then a green mask tint; the paved render appears over the tapped area. Expected errors also verifiable: without consent the file input is disabled; junk file → Bulgarian error.

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): public page with upload and tap-to-segment flow"
```

---

### Task 11: Product panel, editing toolbar, perspective handles, compare, actions

**Files:**
- Create: `src/NaturalStoneImpex.Client/Components/VisualizerProductPanel.razor`
- Modify: `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`
- Modify: `src/NaturalStoneImpex.Client/wwwroot/css/app.css` (append)
- Modify: `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js` (one helper)

**Interfaces:**
- Consumes: everything from Tasks 7–10; `CartService.AddItem(CartItem)` (existing).
- Produces: `<VisualizerProductPanel Products="..." SelectedId="..." Disabled="..." OnSelect="..." />`; JS helper `nsiVisualizer.getStageRect()` → `{ left, top, width, height }` (CSS pixels of the photo element, for handle-drag math).

- [ ] **Step 1: Add the JS helper**

In `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`, next to the other `api.*` functions add:

```javascript
  api.getStageRect = function () {
    var r = photoImg.getBoundingClientRect();
    return { left: r.left, top: r.top, width: r.width, height: r.height };
  };
```

- [ ] **Step 2: Create the product panel component**

Create `src/NaturalStoneImpex.Client/Components/VisualizerProductPanel.razor`:

```razor
@using NaturalStoneImpex.Client.Models

<div class="card">
    <div class="card-header">Изберете настилка</div>
    <div class="card-body p-2">
        <input class="form-control form-control-sm mb-2" placeholder="Търсене…"
               value="@_search" @oninput="e => _search = e.Value?.ToString() ?? string.Empty" />
        <select class="form-select form-select-sm mb-2" @bind="_categoryId">
            <option value="0">Всички категории</option>
            @foreach (var category in Products.Select(p => new { p.CategoryId, p.CategoryName }).Distinct())
            {
                <option value="@category.CategoryId">@category.CategoryName</option>
            }
        </select>
        <div class="viz-product-list list-group">
            @foreach (var product in Filtered)
            {
                <button type="button"
                        class="list-group-item list-group-item-action d-flex align-items-center gap-2 @(product.Id == SelectedId ? "active" : "")"
                        disabled="@Disabled"
                        @onclick="() => OnSelect.InvokeAsync(product)">
                    <img src="@(product.ImagePath ?? product.TexturePath)" alt="@product.Name"
                         class="viz-product-thumb" />
                    <span class="flex-grow-1 text-start">@product.Name</span>
                    <span class="text-nowrap">@product.PriceWithVat.ToString("F2") € / @product.UnitDisplay</span>
                </button>
            }
            @if (!Filtered.Any())
            {
                <div class="text-muted small p-2">Няма продукти, отговарящи на търсенето.</div>
            }
        </div>
    </div>
</div>

@code {
    [Parameter] public List<VisualizerProductDto> Products { get; set; } = new();
    [Parameter] public int? SelectedId { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public EventCallback<VisualizerProductDto> OnSelect { get; set; }

    private string _search = string.Empty;
    private int _categoryId;

    private IEnumerable<VisualizerProductDto> Filtered =>
        Products.Where(p =>
            (_categoryId == 0 || p.CategoryId == _categoryId) &&
            (string.IsNullOrWhiteSpace(_search) ||
             p.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)));
}
```

- [ ] **Step 3: Extend the Visualizer page**

In `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`:

1. Add injections at the top (after the existing `@inject` lines):

```razor
@inject CartService CartService
```

2. Replace the workspace markup (the whole `else { ... }` block after the upload card) with a two-column layout:

```razor
else
{
    <div class="row g-3">
        <div class="col-lg-8">
            @if (!_hasMask)
            {
                <div class="alert alert-primary py-2">Докоснете областта, която искате да покриете с настилка.</div>
            }

            <div class="btn-toolbar gap-2 mb-2" role="toolbar" aria-label="Инструменти">
                <div class="btn-group btn-group-sm" role="group">
                    <button class="btn @(ModeButton("tap-add"))" @onclick='() => SetModeAsync("tap-add")' disabled="@_busy">Добави област</button>
                    <button class="btn @(ModeButton("tap-remove"))" @onclick='() => SetModeAsync("tap-remove")' disabled="@(_busy || !_hasMask)">Премахни</button>
                    <button class="btn @(ModeButton("brush"))" @onclick='() => SetModeAsync("brush")' disabled="@_busy">Четка</button>
                    <button class="btn @(ModeButton("erase"))" @onclick='() => SetModeAsync("erase")' disabled="@(_busy || !_hasMask)">Гума</button>
                </div>
                <button class="btn btn-sm btn-outline-danger" @onclick="ClearMaskAsync" disabled="@(_busy || !_hasMask)">Изчисти</button>
                <button class="btn btn-sm @(_showHandles ? "btn-secondary" : "btn-outline-secondary")"
                        @onclick="ToggleHandlesAsync" disabled="@(_busy || !_hasMask)">Перспектива</button>
                @if (_mode is "brush" or "erase")
                {
                    <div class="d-flex align-items-center gap-1">
                        <label class="small text-nowrap" for="viz-brush">Размер на четката</label>
                        <input id="viz-brush" type="range" min="10" max="120" step="5" value="@_brushSize"
                               @oninput="OnBrushSizeChanged" />
                    </div>
                }
            </div>

            <div class="position-relative" @onpointermove="OnHandleMove" @onpointerup="OnHandleUp">
                <div id="viz-stage"></div>
                @if (_showHandles && _photoW > 0)
                {
                    <svg class="viz-handles" viewBox="0 0 @_photoW @_photoH" preserveAspectRatio="none">
                        <polygon points="@HandlePolygon" class="viz-grid-outline" />
                        <line x1="@_corners[0]" y1="@_corners[1]" x2="@_corners[6]" y2="@_corners[7]" class="viz-grid-line" />
                        <line x1="@_corners[2]" y1="@_corners[3]" x2="@_corners[4]" y2="@_corners[5]" class="viz-grid-line" />
                        @for (var i = 0; i < 4; i++)
                        {
                            var index = i;
                            <circle cx="@_corners[index * 2]" cy="@_corners[index * 2 + 1]" r="@(_photoW * 0.02)"
                                    class="viz-handle" @onpointerdown="e => OnHandleDown(e, index)"
                                    @onpointerdown:preventDefault @onpointerdown:stopPropagation />
                        }
                    </svg>
                }
                @if (_busy)
                {
                    <div class="viz-overlay d-flex align-items-center justify-content-center">
                        <div class="text-center text-white">
                            <div class="spinner-border mb-2" role="status"></div>
                            <div>Разпознаваме областта…</div>
                        </div>
                    </div>
                }
            </div>

            @if (_hasMask)
            {
                <div class="row g-2 mt-1">
                    <div class="col-sm-4">
                        <label class="form-label small mb-0" for="viz-scale">Размер на камъка</label>
                        <input id="viz-scale" type="range" class="form-range" min="0.3" max="3" step="0.05"
                               value="@_scale" @oninput="OnScaleChanged" />
                    </div>
                    <div class="col-sm-4">
                        <label class="form-label small mb-0" for="viz-rot">Завъртане</label>
                        <input id="viz-rot" type="range" class="form-range" min="0" max="90" step="1"
                               value="@_rotation" @oninput="OnRotationChanged" />
                    </div>
                    <div class="col-sm-4">
                        <label class="form-label small mb-0" for="viz-cmp">Преди / След</label>
                        <input id="viz-cmp" type="range" class="form-range" min="0" max="100" step="1"
                               value="@_compare" @oninput="OnCompareChanged" />
                    </div>
                </div>

                <div class="d-flex flex-wrap gap-2 mt-2">
                    <button class="btn btn-outline-primary" @onclick="DownloadAsync" disabled="@_busy">Изтегли изображението</button>
                    <button class="btn btn-success" @onclick="AddToCart" disabled="@(_busy || _selected is null)">Добави в количката</button>
                    @if (_selected is not null)
                    {
                        <a class="btn btn-outline-secondary" href="/products/@_selected.Id">Виж продукта</a>
                    }
                </div>
                @if (_cartMessage is not null)
                {
                    <div class="alert alert-success py-2 mt-2">@_cartMessage</div>
                }
            }

            <p class="text-muted small mt-2 mb-1">
                Визуализацията е ориентировъчна. Реалният продукт може да се различава по цвят и вид,
                а размерите са приблизителни.
            </p>

            @if (_error is not null)
            {
                <div class="alert alert-danger mt-2">@_error</div>
            }

            <button class="btn btn-outline-secondary mt-2" @onclick="ResetAsync" disabled="@_busy">Нова снимка</button>
        </div>

        <div class="col-lg-4">
            <VisualizerProductPanel Products="_products"
                                    SelectedId="_selected?.Id"
                                    Disabled="@(_busy || !_hasMask)"
                                    OnSelect="OnProductSelectedAsync" />
        </div>
    </div>
}
```

3. Add the new state fields and methods to `@code`:

```csharp
    private string _mode = "tap-add";
    private int _brushSize = 40;
    private bool _showHandles;
    private double[] _corners = new double[8];
    private int _photoW, _photoH;
    private int _dragIndex = -1;
    private double _scale = 1.0;
    private double _rotation;
    private double _compare;
    private string? _cartMessage;

    private string ModeButton(string mode) =>
        _mode == mode ? "btn-primary" : "btn-outline-primary";

    private string HandlePolygon =>
        $"{_corners[0]},{_corners[1]} {_corners[2]},{_corners[3]} {_corners[4]},{_corners[5]} {_corners[6]},{_corners[7]}";

    private async Task SetModeAsync(string mode)
    {
        _mode = mode;
        await JS.InvokeVoidAsync("nsiVisualizer.setMode", mode);
    }

    private async Task OnBrushSizeChanged(ChangeEventArgs e)
    {
        _brushSize = int.Parse(e.Value?.ToString() ?? "40");
        await JS.InvokeVoidAsync("nsiVisualizer.setBrushSize", _brushSize);
    }

    private async Task ClearMaskAsync()
    {
        await JS.InvokeVoidAsync("nsiVisualizer.clearMask");
        _points.Clear();
        _hasMask = false;
        _showHandles = false;
        await SetModeAsync("tap-add");
    }

    private async Task ToggleHandlesAsync()
    {
        _showHandles = !_showHandles;
        if (_showHandles)
        {
            var corners = await JS.InvokeAsync<double[]>("nsiVisualizer.defaultCornersFromMask");
            if (_corners[2] == 0 && _corners[5] == 0) _corners = corners; // keep user-adjusted values
        }
    }

    private void OnHandleDown(PointerEventArgs e, int index) => _dragIndex = index;

    private async Task OnHandleMove(PointerEventArgs e)
    {
        if (_dragIndex < 0) return;
        var rect = await JS.InvokeAsync<StageRect>("nsiVisualizer.getStageRect");
        _corners[_dragIndex * 2] = Math.Clamp((e.ClientX - rect.Left) / rect.Width * _photoW, 0, _photoW);
        _corners[_dragIndex * 2 + 1] = Math.Clamp((e.ClientY - rect.Top) / rect.Height * _photoH, 0, _photoH);
        await JS.InvokeVoidAsync("nsiVisualizer.setCorners", (object)_corners);
        await JS.InvokeVoidAsync("nsiVisualizer.render");
    }

    private void OnHandleUp(PointerEventArgs e) => _dragIndex = -1;

    private async Task OnScaleChanged(ChangeEventArgs e)
    {
        _scale = double.Parse(e.Value?.ToString() ?? "1", System.Globalization.CultureInfo.InvariantCulture);
        await JS.InvokeVoidAsync("nsiVisualizer.setScale", _scale);
        await JS.InvokeVoidAsync("nsiVisualizer.render");
    }

    private async Task OnRotationChanged(ChangeEventArgs e)
    {
        _rotation = double.Parse(e.Value?.ToString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        await JS.InvokeVoidAsync("nsiVisualizer.setRotation", _rotation);
        await JS.InvokeVoidAsync("nsiVisualizer.render");
    }

    private async Task OnCompareChanged(ChangeEventArgs e)
    {
        _compare = double.Parse(e.Value?.ToString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        await JS.InvokeVoidAsync("nsiVisualizer.setCompareRatio", _compare);
    }

    private async Task OnProductSelectedAsync(VisualizerProductDto product)
    {
        _selected = product;
        _cartMessage = null;
        await ApplySelectedProductAsync();
    }

    private async Task DownloadAsync() =>
        await JS.InvokeVoidAsync("nsiVisualizer.downloadResult", "vizualizacia.jpg");

    private void AddToCart()
    {
        if (_selected is null) return;
        CartService.AddItem(new CartItem
        {
            ProductId = _selected.Id,
            ProductName = _selected.Name,
            UnitPriceWithVat = _selected.PriceWithVat,
            VatAmount = _selected.VatAmount,
            UnitPriceWithoutVat = _selected.PriceWithoutVat,
            Unit = _selected.Unit,
            UnitDisplay = _selected.UnitDisplay,
            Quantity = 1,
            ImagePath = _selected.ImagePath
        });
        _cartMessage = "Продуктът е добавен в количката.";
    }

    private record StageRect(double Left, double Top, double Width, double Height);
```

4. In `OnPhotoSelectedAsync` (or `InitStageAsync`), keep the photo dimensions returned by `loadPhotoFromDataUrl` — change the load call in `InitStageAsync` to:

```csharp
        var size = await JS.InvokeAsync<PhotoSize>("nsiVisualizer.loadPhotoFromDataUrl", dataUrl);
        _photoW = size.Width;
        _photoH = size.Height;
```

and add `private record PhotoSize(int Width, int Height);` to `@code`.

5. In `ResetAsync`, also reset the new state: `_mode = "tap-add"; _showHandles = false; _corners = new double[8]; _scale = 1.0; _rotation = 0; _compare = 0; _cartMessage = null;`.

- [ ] **Step 4: Append the styles**

Append to the stylesheet from Task 10 Step 2:

```css
.viz-handles {
    position: absolute;
    inset: 0;
    width: 100%;
    height: 100%;
}
.viz-grid-outline {
    fill: rgba(13, 110, 253, 0.08);
    stroke: rgba(13, 110, 253, 0.9);
    stroke-width: 2;
    vector-effect: non-scaling-stroke;
}
.viz-grid-line {
    stroke: rgba(13, 110, 253, 0.5);
    stroke-width: 1;
    vector-effect: non-scaling-stroke;
}
.viz-handle {
    fill: #0d6efd;
    stroke: #fff;
    stroke-width: 2;
    vector-effect: non-scaling-stroke;
    cursor: grab;
    pointer-events: all;
}
.viz-product-list {
    max-height: 480px;
    overflow-y: auto;
}
.viz-product-thumb {
    width: 48px;
    height: 48px;
    object-fit: cover;
    border-radius: 4px;
}
```

- [ ] **Step 5: Build and verify manually**

`dotnet build`, run API + client, open `/visualizer`, verify: product switching re-renders instantly and highlights the active product; «Премахни» + tap shrinks the mask; brush/eraser edit it; «Перспектива» shows the draggable quad and dragging updates the render live; the three sliders work; «Изтегли» downloads a JPEG; «Добави в количката» updates the cart badge; «Виж продукта» navigates.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): product panel, mask tools, perspective handles, compare and actions"
```

---

### Task 12: Entry points — navigation, product detail button, home promo

**Files:**
- Modify: `src/NaturalStoneImpex.Client/Layout/MainLayout.razor` (nav list, ~line 26–35)
- Modify: `src/NaturalStoneImpex.Client/Pages/Public/ProductDetail.razor`
- Modify: `src/NaturalStoneImpex.Client/Pages/Public/Home.razor`

- [ ] **Step 1: Navigation link**

In `src/NaturalStoneImpex.Client/Layout/MainLayout.razor`, after the «Каталог» `<li>` add:

```razor
                <li class="nav-item">
                    <NavLink class="nav-link" href="/visualizer">
                        Визуализатор
                    </NavLink>
                </li>
```

- [ ] **Step 2: Product detail button**

In `src/NaturalStoneImpex.Client/Pages/Public/ProductDetail.razor`, locate the add-to-cart block (`AddToCart` button area, around line 150 of the current file) and add below it, inside the same markup section:

```razor
            @if (_product.IsVisualizerEnabled)
            {
                <a class="btn btn-outline-primary mt-2" href="/visualizer?productId=@_product.Id">
                    Виж как ще изглежда при вас
                </a>
            }
```

(`IsVisualizerEnabled` exists on the client `ProductDto` since Task 9.)

- [ ] **Step 3: Home page promo**

In `src/NaturalStoneImpex.Client/Pages/Public/Home.razor`, after the existing hero/CTA section (adapt placement to the file's current structure — it is a short page), add:

```razor
<section class="my-4">
    <div class="card bg-light">
        <div class="card-body d-flex flex-wrap align-items-center justify-content-between gap-3">
            <div>
                <h5 class="card-title mb-1">Вижте настилката във вашия двор</h5>
                <p class="card-text mb-0">
                    Качете снимка на вашата алея или двор и разгледайте как ще изглежда с нашите естествени камъни.
                </p>
            </div>
            <a class="btn btn-primary" href="/visualizer">Опитай визуализатора</a>
        </div>
    </div>
</section>
```

- [ ] **Step 4: Build, verify, commit**

`dotnet build`, run both projects: nav shows «Визуализатор» on desktop + mobile hamburger; product detail of an enabled product shows the button and preselects that product in the visualizer; home промо links correctly.

```powershell
git add -A
git commit -m "feat(visualizer): navigation, product detail and home entry points"
```

---

### Task 13: Admin product form — visualizer fields and texture upload

**Files:**
- Modify: `src/NaturalStoneImpex.Client/Services/IProductService.cs`
- Modify: `src/NaturalStoneImpex.Client/Services/ProductService.cs`
- Modify: `src/NaturalStoneImpex.Client/Pages/Admin/ProductForm.razor`

**Interfaces:**
- Consumes: `POST /api/products/{id}/texture` (Task 2); client request models with `IsVisualizerEnabled`/`TextureWidthMeters` (Task 9).
- Produces: `IProductService.UploadTextureAsync(int id, Stream fileStream, string fileName)` → `Task<string?>` (null = success, otherwise Bulgarian error).

- [ ] **Step 1: Client service method**

In `src/NaturalStoneImpex.Client/Services/IProductService.cs` add:

```csharp
    Task<string?> UploadTextureAsync(int id, Stream fileStream, string fileName);
```

In `src/NaturalStoneImpex.Client/Services/ProductService.cs` add (mirrors the existing `UploadImageAsync`, different endpoint and form field name `texture`):

```csharp
    public async Task<string?> UploadTextureAsync(int id, Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        var contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "texture", fileName);

        var response = await _httpClient.PostAsync($"api/products/{id}/texture", content);

        if (!response.IsSuccessStatusCode)
        {
            return await ExtractErrorAsync(response);
        }

        return null;
    }
```

- [ ] **Step 2: Product form fields**

Open `src/NaturalStoneImpex.Client/Pages/Admin/ProductForm.razor` and follow its existing structure (the form binds a request model and has an image `InputFile` block — replicate that pattern):

1. In the form markup, after the existing image upload block, add:

```razor
            <hr />
            <h6>Визуализатор</h6>
            <div class="form-check form-switch mb-2">
                <input class="form-check-input" type="checkbox" id="viz-enabled" @bind="_model.IsVisualizerEnabled">
                <label class="form-check-label" for="viz-enabled">Достъпен във визуализатора</label>
            </div>
            <div class="mb-2">
                <label class="form-label" for="viz-width">Реална ширина на текстурата (м)</label>
                <input id="viz-width" type="number" class="form-control" step="0.01" min="0.1" max="100"
                       @bind="_model.TextureWidthMeters" />
            </div>
            @if (_isEdit)
            {
                <div class="mb-2">
                    <label class="form-label">Текстура за визуализатора (безшевна)</label>
                    @if (!string.IsNullOrEmpty(_texturePath))
                    {
                        <div class="mb-1"><img src="@_texturePath" alt="Текстура" style="max-width: 120px;" /></div>
                    }
                    <InputFile OnChange="OnTextureSelected" accept=".jpg,.jpeg,.png" class="form-control" />
                    <div class="form-text">
                        Снимайте продукта отгоре при равномерна светлина. За безшевна текстура използвайте
                        напр. GIMP: Filters → Map → Make Seamless.
                    </div>
                </div>
            }
            else
            {
                <div class="form-text mb-2">Текстурата се качва след създаване на продукта (в режим на редакция).</div>
            }
```

`_model` here is the form's bound request object (`CreateProductRequest`/`UpdateProductRequest` — adapt to the actual field name used in the file). `_isEdit` is the form's existing create/edit flag (adapt to the actual name).

2. In `@code`, add texture state and handler, and wire the upload into the existing save flow the same way the image upload is wired (after a successful create/update, if a texture file was chosen, upload it and surface any error through the form's existing error display):

```csharp
    private IBrowserFile? _textureFile;
    private string? _texturePath; // set from the loaded ProductDto.TextureImagePath (resolve like the image path)

    private void OnTextureSelected(InputFileChangeEventArgs e) => _textureFile = e.File;

    private async Task<string?> UploadTextureIfSelectedAsync(int productId)
    {
        if (_textureFile is null) return null;
        await using var stream = _textureFile.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
        return await ProductService.UploadTextureAsync(productId, stream, _textureFile.Name);
    }
```

3. When loading an existing product into the form, populate `_model.IsVisualizerEnabled`, `_model.TextureWidthMeters`, and `_texturePath` from the `ProductDto` fields (Task 9).

- [ ] **Step 3: Build and verify manually**

`dotnet build`; run both projects; in `/admin` edit a product: upload a texture, set width 1.2, tick «Достъпен във визуализатора», save. Verify `GET /api/visualizer/products` now returns it and it appears in the visualizer's product panel. Also verify the guard: on a product without texture, ticking the switch and saving shows «За да включите продукта във визуализатора, първо качете текстура.» (upload happens after save on create — enable requires a second save, which the guard message makes clear).

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): admin product form texture and visualizer fields"
```

---

### Task 14: Retention job, documentation, and E2E checklist

**Files:**
- Create: `src/NaturalStoneImpex.Api/Services/Segmentation/VisualizationRequestCleanupService.cs`
- Modify: `src/NaturalStoneImpex.Api/Program.cs`
- Modify: `docs/api-endpoints.md`
- Modify: `docs/database-schema.md`
- Modify: `CLAUDE.md` (commands section)

- [ ] **Step 1: Quota-row retention job**

Create `src/NaturalStoneImpex.Api/Services/Segmentation/VisualizationRequestCleanupService.cs` (spec §7.2: prune rows older than 90 days — they hold no personal data, this is just hygiene):

```csharp
using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Data;

namespace NaturalStoneImpex.Api.Services.Segmentation;

public class VisualizationRequestCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VisualizationRequestCleanupService> _logger;

    public VisualizationRequestCleanupService(IServiceScopeFactory scopeFactory,
        ILogger<VisualizationRequestCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = DateTime.UtcNow.AddDays(-90);
                var removed = await db.VisualizationRequests
                    .Where(r => r.CreatedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);
                if (removed > 0)
                    _logger.LogInformation("Pruned {Count} visualization request rows", removed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Visualization request cleanup failed");
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

Register in `src/NaturalStoneImpex.Api/Program.cs` next to the other visualizer registrations:

```csharp
builder.Services.AddHostedService<VisualizationRequestCleanupService>();
```

Run `dotnet build` + `dotnet test` — all green.

- [ ] **Step 2: Update the API contract doc**

In `docs/api-endpoints.md`, add a `### Visualizer` section following the document's existing table style:

```markdown
### Visualizer (Визуализатор)

| Method | Endpoint                                | Description                                        | Auth  |
|--------|-----------------------------------------|----------------------------------------------------|-------|
| GET    | /api/visualizer/products                | Visualizer-enabled products with texture info      | No    |
| POST   | /api/visualizer/segment                 | Segment uploaded photo (multipart: photo + points) | No    |
| POST   | /api/visualizer/segment/{sessionToken}  | Refine mask with additional points (JSON body)     | No    |
| POST   | /api/products/{id}/texture              | Upload product texture image                       | Admin |

`POST /api/visualizer/segment` responses: `200 { sessionToken, maskPng, width, height }`,
`400` invalid photo/points, `429` daily quota reached, `503` visualizer disabled or busy.
`POST /api/visualizer/segment/{sessionToken}` additionally returns `404` when the server-side
embedding cache has expired (client re-uploads the photo). Photos are processed in memory and
never stored; quotas are enforced per hashed IP per day (see `Visualizer` section in appsettings).
```

- [ ] **Step 3: Update the database schema doc**

In `docs/database-schema.md`, add the three `Product` columns (`IsVisualizerEnabled bit NOT NULL DEFAULT 0`, `TextureImagePath nvarchar(500) NULL`, `TextureWidthMeters decimal(18,2) NOT NULL DEFAULT 1.00`) to the Product table definition, and append a `VisualizationRequests` table section (`Id int PK`, `IpHash nvarchar(64)`, `Status int`, `DurationMs int`, `CreatedAt datetime2`, indexes on `(IpHash, CreatedAt)` and `CreatedAt`), following the document's existing format.

- [ ] **Step 4: Document the model download in CLAUDE.md**

In `CLAUDE.md`'s Commands section add:

```bash
# Download visualizer ONNX models (one-time, required for the visualizer feature)
powershell -File scripts/download-visualizer-models.ps1
```

- [ ] **Step 5: Full E2E checklist (manual)**

Run API + client with models downloaded and at least two visualizer-enabled products, then walk through:

1. `/visualizer` from nav — consent gate blocks upload until checked.
2. Upload a real outdoor photo (from a phone if possible) — tap the driveway → mask appears ≤ 6 s.
3. «Премахни» tap on an over-segmented region shrinks the mask; brush/eraser fine-tune it.
4. «Перспектива» — drag corners; stones follow; scale/rotation sliders work.
5. Switch products repeatedly — updates feel instant (< 0.5 s), no network calls in DevTools.
6. Compare slider, «Изтегли» (valid JPEG), «Добави в количката» (badge updates), «Виж продукта».
7. Wait 16+ minutes (or set `EmbeddingCacheMinutes: 0` temporarily), tap again — flow recovers transparently via re-upload.
8. Set `PerIpDailyLimit: 1` temporarily — second photo upload shows the Bulgarian quota message.
9. Set `Enabled: false` — page shows service-unavailable behavior (tap → error alert; feature degrades to brush-only marking).
10. DevTools device emulation (phone): camera capture input, bottom layout, touch brush and handles usable.
11. Chrome with `--disable-webgl` (or harness `?fallback=1`): canvas-2D fallback renders.
12. Verify nothing was written to `wwwroot/uploads` during customer flows and `VisualizationRequests` has one row per uploaded photo.

Record any failures as issues; do not ship with a failing checklist item.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): retention job, docs and E2E checklist"
```

---

## Post-plan notes for the implementer

- **Model contract risk (highest-risk item, surfaces in Task 3):** ONNX exports vary in tensor names/shapes. The wrapper reads input names dynamically and feeds only declared inputs, and the integration test pins behavior. If the chosen export deviates beyond that flexibility, fix the wrapper (not the callers) — `ISamModel` is the stable boundary.
- **Perspective defaults (`GROUND_H = 15`, top edge 45%)** are heuristics tuned for typical slightly-downward yard photos. If early testing shows stones too stretched/compressed near the top, tune `GROUND_H` first; the user-facing handles compensate for individual photos.
- **Spec traceability:** spec §3 flows → Tasks 10–12; §5.1 → Tasks 3–6; §5.2–5.3 → Task 7; §5.4 ladder → Tasks 8, 10 (brush fallback on 503, canvas-2D on no-WebGL); §7 → Tasks 1, 5; §8 → Task 13; §9 quotas/latency → Tasks 5–6; §10 privacy (in-memory only, no third parties) → Tasks 5–6; §7.2 pruning + docs → Task 14.



