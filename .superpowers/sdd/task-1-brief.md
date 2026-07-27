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

