# Visualizer «Фотореалистичен режим» (Option 4 — Full Generative Edit) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a generative-AI visualization mode to the visualizer: the customer photo + product reference images are sent (server-mediated) to the Gemini image-editing API, which returns a photorealistic re-paved photo; product switches regenerate via a stored photo token with disk-cached results.

**Architecture:** Blazor WASM never talks to the AI provider (CORS + key security) — a new `GenerativeVisualizationService` in the API validates/re-encodes the photo (ImageSharp, EXIF stripped), enforces per-IP/global daily quotas via the `VisualizationRequest` table, calls `IImageEditProvider` (Gemini adapter), and stores results under `wwwroot/uploads/visualizer/{photoToken}/` served as capability URLs by static files. An hourly `BackgroundService` deletes photos older than 48 h.

**Tech Stack:** ASP.NET Core 8 Web API, EF Core (SQL Server; InMemory for tests), SixLabors.ImageSharp, built-in `Microsoft.AspNetCore.RateLimiting`, Blazor WebAssembly + Bootstrap 5, xUnit.

**Spec:** `docs/visualizer-specification-option4.md`

## Global Constraints

- All UI text in Bulgarian; all error responses exactly `{ "error": "Българско съобщение" }`.
- Controllers are thin — logic in `Services/`; never expose EF entities — DTOs only.
- C# naming: PascalCase public, `_camelCase` private fields; `var` when obvious; nullable reference types enabled; async/await only (no `.Result`/`.Wait()`).
- Decimal precision: `decimal(18,2)` for prices/quantities, `decimal(18,4)` for `ProviderCostEstimate`.
- Migration command: `dotnet ef migrations add <Name> --project src/NaturalStoneImpex.Api` (run from repo root).
- The Gemini API key is NEVER committed — dev: `dotnet user-secrets`, prod: environment variable `Visualizer__Generative__ApiKey`.
- `Visualizer:Generative:Enabled` defaults to `false` — the feature ships dark.
- Solution file: `src/NaturalStoneImpex.sln`. Build: `dotnet build src/NaturalStoneImpex.sln`. Tests: `dotnet test src/NaturalStoneImpex.sln`.

## Prerequisite Contract (from the Option 2 implementation)

This plan is designed to run **after** the Option 2 visualizer (`docs/visualizer-specification.md`) is implemented. It assumes:

1. `Product` already has `IsVisualizerEnabled` (bool) and `TextureImagePath` (string?) columns. **If missing** (Option 2 not yet built): add these two properties in Task 2 alongside `VisualizerPromptHint`, with the same max lengths (`TextureImagePath` → 500).
2. `VisualizationRequest` entity + `DbSet<VisualizationRequest>` exist (Option 2 spec §7.2: `Id`, `IpHash`, `Status`, `DurationMs`, `CreatedAt`). **If missing**: create the full entity as shown in Task 2 (it is the superset).
3. `VisualizerController` exists at route `api/visualizer` with `GET /api/visualizer/products`. **If missing**: Task 7 creates the controller with only the generate endpoint; the client panel then needs a product list from the existing `GET /api/products?` endpoint instead.
4. Client: `Pages/Public/Visualizer.razor` page with photo upload state and product side panel exists. **If missing**: host `GenerativeVisualizerPanel` (Task 11) on a minimal new page with an upload input — the panel is self-contained by design.
5. `tests/NaturalStoneImpex.Api.Tests` xUnit project exists. **If missing**: Task 1 creates it (Task 1 is skippable otherwise).

If a contract item's actual names differ in the Option 2 implementation, adapt references at execution time and note the deviation in the commit message.

---

### Task 1: Test project (skip if `tests/NaturalStoneImpex.Api.Tests` already exists)

**Files:**
- Create: `tests/NaturalStoneImpex.Api.Tests/` (project via `dotnet new`)
- Create: `tests/NaturalStoneImpex.Api.Tests/TestHelpers.cs`

**Interfaces:**
- Consumes: `NaturalStoneImpex.Api` project reference.
- Produces: `TestHelpers.CreateDb()` → `AppDbContext` (InMemory); `TestHelpers.CreateEnv(string root)` → `IWebHostEnvironment`; `TestHelpers.CreateJpeg(int w = 64, int h = 64)` → `byte[]`; `TestHelpers.CreateFormFile(byte[] bytes, string name = "photo", string fileName = "test.jpg", string contentType = "image/jpeg")` → `IFormFile`.

- [ ] **Step 1: Create the project and wire it into the solution**

```bash
dotnet new xunit -o tests/NaturalStoneImpex.Api.Tests
dotnet sln src/NaturalStoneImpex.sln add tests/NaturalStoneImpex.Api.Tests
dotnet add tests/NaturalStoneImpex.Api.Tests reference src/NaturalStoneImpex.Api
dotnet add tests/NaturalStoneImpex.Api.Tests package Microsoft.EntityFrameworkCore.InMemory
dotnet add src/NaturalStoneImpex.Api package SixLabors.ImageSharp
```

(ImageSharp is added to the API project here because `TestHelpers.CreateJpeg` uses it transitively from the next step onward; the API itself uses it from Task 5.)

- [ ] **Step 2: Replace `tests/NaturalStoneImpex.Api.Tests/UnitTest1.cs` with `TestHelpers.cs`**

Delete `UnitTest1.cs`, create `TestHelpers.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using NaturalStoneImpex.Api.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NaturalStoneImpex.Api.Tests;

public static class TestHelpers
{
    public static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    public static IWebHostEnvironment CreateEnv(string webRootPath)
    {
        Directory.CreateDirectory(webRootPath);
        return new FakeWebHostEnvironment { WebRootPath = webRootPath };
    }

    public static byte[] CreateJpeg(int width = 64, int height = 64)
    {
        using var image = new Image<Rgb24>(width, height);
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    public static IFormFile CreateFormFile(byte[] bytes, string name = "photo",
        string fileName = "test.jpg", string contentType = "image/jpeg")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, name, fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "NaturalStoneImpex.Api.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
    }
}
```

- [ ] **Step 3: Verify the solution builds and the (empty) test suite runs**

Run: `dotnet test src/NaturalStoneImpex.sln`
Expected: build succeeds, 0 tests, exit code 0.

- [ ] **Step 4: Commit**

```bash
git add tests/ src/NaturalStoneImpex.sln src/NaturalStoneImpex.Api/NaturalStoneImpex.Api.csproj
git commit -m "test: add API test project with InMemory EF and image helpers"
```

---

### Task 2: Data model — `VisualizationRequest` generative columns + `Product.VisualizerPromptHint` + migration

**Files:**
- Modify (or Create per Prerequisite Contract #2): `src/NaturalStoneImpex.Api/Models/Entities/VisualizationRequest.cs`
- Modify: `src/NaturalStoneImpex.Api/Models/Entities/Product.cs`
- Modify: `src/NaturalStoneImpex.Api/Data/AppDbContext.cs`
- Test: `tests/NaturalStoneImpex.Api.Tests/VisualizationRequestModelTests.cs`

**Interfaces:**
- Produces: entity `VisualizationRequest` with `Mode` (`VisualizerMode`), `IpHash`, `Status` (`VisualizationStatus`), `DurationMs`, `PhotoToken?`, `ProductId?`, `ProviderCostEstimate?`, `ConsentGiven`, `CreatedAt`; enums `VisualizerMode { Segmentation = 0, Generative = 1 }`, `VisualizationStatus { Succeeded = 0, Failed = 1 }`; `Product.VisualizerPromptHint` (string?). `DbSet<VisualizationRequest> VisualizationRequests` on `AppDbContext`.

- [ ] **Step 1: Write the failing test**

Create `tests/NaturalStoneImpex.Api.Tests/VisualizationRequestModelTests.cs`:

```csharp
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Tests;

public class VisualizationRequestModelTests
{
    [Fact]
    public async Task VisualizationRequest_GenerativeRow_RoundTrips()
    {
        using var db = TestHelpers.CreateDb();
        db.VisualizationRequests.Add(new VisualizationRequest
        {
            Mode = VisualizerMode.Generative,
            IpHash = new string('a', 64),
            Status = VisualizationStatus.Succeeded,
            DurationMs = 4200,
            PhotoToken = Guid.NewGuid().ToString("N"),
            ProviderCostEstimate = 0.07m,
            ConsentGiven = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var row = db.VisualizationRequests.Single();
        Assert.Equal(VisualizerMode.Generative, row.Mode);
        Assert.Equal(0.07m, row.ProviderCostEstimate);
        Assert.True(row.ConsentGiven);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/NaturalStoneImpex.sln --filter VisualizationRequestModelTests`
Expected: FAIL — compilation error (missing members / missing entity).

- [ ] **Step 3: Write the entity (full target shape — superset of Option 2's)**

`src/NaturalStoneImpex.Api/Models/Entities/VisualizationRequest.cs`:

```csharp
namespace NaturalStoneImpex.Api.Models.Entities;

public enum VisualizerMode
{
    Segmentation = 0,
    Generative = 1
}

public enum VisualizationStatus
{
    Succeeded = 0,
    Failed = 1
}

public class VisualizationRequest
{
    public int Id { get; set; }
    public VisualizerMode Mode { get; set; }
    public string IpHash { get; set; } = string.Empty;
    public VisualizationStatus Status { get; set; }
    public int DurationMs { get; set; }
    public string? PhotoToken { get; set; }
    public int? ProductId { get; set; }
    public decimal? ProviderCostEstimate { get; set; }
    public bool ConsentGiven { get; set; }
    public DateTime CreatedAt { get; set; }

    public Product? Product { get; set; }
}
```

If the file already exists from Option 2, add only the missing members (`Mode`, `PhotoToken`, `ProductId`, `ProviderCostEstimate`, `ConsentGiven`, `Product`) — do not remove Option 2's members.

- [ ] **Step 4: Add `VisualizerPromptHint` to `Product`**

In `src/NaturalStoneImpex.Api/Models/Entities/Product.cs`, after the `ImagePath` property (and after Option 2's `TextureImagePath`/`IsVisualizerEnabled` if present):

```csharp
    public string? VisualizerPromptHint { get; set; }
```

(Per Prerequisite Contract #1: if Option 2 is not implemented yet, also add `public bool IsVisualizerEnabled { get; set; }` and `public string? TextureImagePath { get; set; }` here.)

- [ ] **Step 5: Configure in `AppDbContext`**

In `src/NaturalStoneImpex.Api/Data/AppDbContext.cs` — ensure the DbSet exists:

```csharp
    public DbSet<VisualizationRequest> VisualizationRequests => Set<VisualizationRequest>();
```

Inside the `modelBuilder.Entity<Product>(entity => { ... })` block add:

```csharp
            entity.Property(e => e.VisualizerPromptHint).HasMaxLength(500);
```

(If created here per the contract: also `entity.Property(e => e.TextureImagePath).HasMaxLength(500);`.)

At the end of `OnModelCreating` add (or extend the existing block to match):

```csharp
        modelBuilder.Entity<VisualizationRequest>(entity =>
        {
            entity.Property(e => e.IpHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.PhotoToken).HasMaxLength(64);
            entity.Property(e => e.ProviderCostEstimate).HasPrecision(18, 4);
            entity.HasIndex(e => e.PhotoToken);
            entity.HasIndex(e => new { e.IpHash, e.CreatedAt });
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test src/NaturalStoneImpex.sln --filter VisualizationRequestModelTests`
Expected: PASS (1 test).

- [ ] **Step 7: Create the migration**

Run: `dotnet ef migrations add AddGenerativeVisualizer --project src/NaturalStoneImpex.Api`
Expected: new files under `src/NaturalStoneImpex.Api/Migrations/` containing `AddColumn` for `VisualizerPromptHint` and the `VisualizationRequest` changes. Then `dotnet build src/NaturalStoneImpex.sln` → succeeds.

- [ ] **Step 8: Commit**

```bash
git add src/NaturalStoneImpex.Api tests/
git commit -m "feat: extend data model for generative visualizer mode"
```

---

### Task 3: Configuration — `GenerativeVisualizerOptions` + appsettings + binding

**Files:**
- Create: `src/NaturalStoneImpex.Api/Models/GenerativeVisualizerOptions.cs`
- Modify: `src/NaturalStoneImpex.Api/appsettings.json`
- Modify: `src/NaturalStoneImpex.Api/Program.cs`

**Interfaces:**
- Produces: `GenerativeVisualizerOptions` with `SectionName = "Visualizer:Generative"` and properties `Enabled`, `Provider`, `Model`, `ApiKey`, `MaxUploadBytes`, `MaxImageDimension`, `BurstPerMinute`, `PerIpDailyLimit`, `GlobalDailyLimit`, `RetentionHours`, `ProviderTimeoutSeconds`, `EstimatedCostPerImage`, `IpHashSalt`; registered via `IOptions<GenerativeVisualizerOptions>`.

- [ ] **Step 1: Create the options class**

`src/NaturalStoneImpex.Api/Models/GenerativeVisualizerOptions.cs`:

```csharp
namespace NaturalStoneImpex.Api.Models;

public class GenerativeVisualizerOptions
{
    public const string SectionName = "Visualizer:Generative";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Gemini";
    public string Model { get; set; } = "gemini-3.1-flash-image";
    public string ApiKey { get; set; } = string.Empty;
    public int MaxUploadBytes { get; set; } = 10 * 1024 * 1024;
    public int MaxImageDimension { get; set; } = 2048;
    public int BurstPerMinute { get; set; } = 5;
    public int PerIpDailyLimit { get; set; } = 10;
    public int GlobalDailyLimit { get; set; } = 200;
    public int RetentionHours { get; set; } = 48;
    public int ProviderTimeoutSeconds { get; set; } = 60;
    public decimal EstimatedCostPerImage { get; set; } = 0.07m;
    public string IpHashSalt { get; set; } = "nsi-visualizer";
}
```

- [ ] **Step 2: Add the config section to `appsettings.json`**

After the `"ClientUrl"` entry (add a comma to the preceding line), keeping any existing `"Visualizer"` keys from Option 2 intact — `Generative` nests inside it:

```json
  "Visualizer": {
    "Generative": {
      "Enabled": false,
      "Provider": "Gemini",
      "Model": "gemini-3.1-flash-image",
      "ApiKey": "",
      "MaxUploadBytes": 10485760,
      "MaxImageDimension": 2048,
      "BurstPerMinute": 5,
      "PerIpDailyLimit": 10,
      "GlobalDailyLimit": 200,
      "RetentionHours": 48,
      "ProviderTimeoutSeconds": 60,
      "EstimatedCostPerImage": 0.07
    }
  }
```

- [ ] **Step 3: Bind in `Program.cs`**

After the line `builder.Services.AddScoped<IInvoiceService, InvoiceService>();` add:

```csharp
builder.Services.Configure<GenerativeVisualizerOptions>(
    builder.Configuration.GetSection(GenerativeVisualizerOptions.SectionName));
```

Add `using NaturalStoneImpex.Api.Models;` to the top of `Program.cs`.

- [ ] **Step 4: Set the dev API key via user-secrets (documentation step — no real key committed)**

```bash
dotnet user-secrets init --project src/NaturalStoneImpex.Api
dotnet user-secrets set "Visualizer:Generative:ApiKey" "PLACEHOLDER-SET-REAL-KEY-LOCALLY" --project src/NaturalStoneImpex.Api
```

Expected: `Successfully saved Visualizer:Generative:ApiKey…`. Production uses env var `Visualizer__Generative__ApiKey`.

- [ ] **Step 5: Verify build**

Run: `dotnet build src/NaturalStoneImpex.sln`
Expected: success.

- [ ] **Step 6: Commit**

```bash
git add src/NaturalStoneImpex.Api
git commit -m "feat: add generative visualizer configuration options"
```

---

### Task 4: Provider abstraction + Gemini adapter

**Files:**
- Create: `src/NaturalStoneImpex.Api/Services/IImageEditProvider.cs`
- Create: `src/NaturalStoneImpex.Api/Services/GeminiImageEditProvider.cs`
- Test: `tests/NaturalStoneImpex.Api.Tests/GeminiImageEditProviderTests.cs`

**Interfaces:**
- Consumes: `GenerativeVisualizerOptions` (Task 3).
- Produces:
  - `record ImageEditInput(byte[] CustomerPhoto, string CustomerPhotoMimeType, List<(byte[] Bytes, string MimeType)> ReferenceImages, string Prompt)`
  - `record ImageEditResult(byte[] ImageBytes, string MimeType)`
  - `class ImageEditProviderException : Exception`
  - `interface IImageEditProvider { Task<ImageEditResult> EditAsync(ImageEditInput input, CancellationToken ct); }`
  - `class GeminiImageEditProvider : IImageEditProvider` (typed HttpClient)

- [ ] **Step 1: Write the failing tests**

Create `tests/NaturalStoneImpex.Api.Tests/GeminiImageEditProviderTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Models;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Tests;

public class GeminiImageEditProviderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;
        public string? LastBody;
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return Response;
        }
    }

    private static (GeminiImageEditProvider Provider, FakeHandler Handler) Create()
    {
        var handler = new FakeHandler();
        var options = Options.Create(new GenerativeVisualizerOptions { ApiKey = "test-key" });
        var provider = new GeminiImageEditProvider(new HttpClient(handler), options);
        return (provider, handler);
    }

    private static ImageEditInput SampleInput() => new(
        CustomerPhoto: new byte[] { 1, 2, 3 },
        CustomerPhotoMimeType: "image/jpeg",
        ReferenceImages: new List<(byte[], string)> { (new byte[] { 4, 5 }, "image/png") },
        Prompt: "test prompt");

    [Fact]
    public async Task EditAsync_BuildsCorrectRequest()
    {
        var (provider, handler) = Create();
        var resultBytes = new byte[] { 9, 9, 9 };
        handler.Response.Content = new StringContent(JsonSerializer.Serialize(new
        {
            output_image = new { data = Convert.ToBase64String(resultBytes), mime_type = "image/png" }
        }), Encoding.UTF8, "application/json");

        await provider.EditAsync(SampleInput(), CancellationToken.None);

        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/interactions",
            handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("test-key", handler.LastRequest.Headers.GetValues("x-goog-api-key").Single());

        using var doc = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("gemini-3.1-flash-image", doc.RootElement.GetProperty("model").GetString());
        var parts = doc.RootElement.GetProperty("input").EnumerateArray().ToList();
        Assert.Equal(3, parts.Count);
        Assert.Equal("text", parts[0].GetProperty("type").GetString());
        Assert.Equal("test prompt", parts[0].GetProperty("text").GetString());
        Assert.Equal("image", parts[1].GetProperty("type").GetString());
        Assert.Equal("image/jpeg", parts[1].GetProperty("mime_type").GetString());
        Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3 }), parts[1].GetProperty("data").GetString());
        Assert.Equal("image/png", parts[2].GetProperty("mime_type").GetString());
        Assert.Equal("image", doc.RootElement.GetProperty("response_format").GetProperty("type").GetString());
    }

    [Fact]
    public async Task EditAsync_ParsesOutputImage()
    {
        var (provider, handler) = Create();
        var resultBytes = new byte[] { 9, 8, 7 };
        handler.Response.Content = new StringContent(JsonSerializer.Serialize(new
        {
            output_image = new { data = Convert.ToBase64String(resultBytes), mime_type = "image/png" }
        }), Encoding.UTF8, "application/json");

        var result = await provider.EditAsync(SampleInput(), CancellationToken.None);

        Assert.Equal(resultBytes, result.ImageBytes);
        Assert.Equal("image/png", result.MimeType);
    }

    [Fact]
    public async Task EditAsync_NonSuccessStatus_Throws()
    {
        var (provider, handler) = Create();
        handler.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        { Content = new StringContent("boom") };

        await Assert.ThrowsAsync<ImageEditProviderException>(
            () => provider.EditAsync(SampleInput(), CancellationToken.None));
    }

    [Fact]
    public async Task EditAsync_MissingOutputImage_Throws()
    {
        var (provider, handler) = Create();
        handler.Response.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        await Assert.ThrowsAsync<ImageEditProviderException>(
            () => provider.EditAsync(SampleInput(), CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/NaturalStoneImpex.sln --filter GeminiImageEditProviderTests`
Expected: FAIL — compilation errors (types not defined).

- [ ] **Step 3: Write the contract**

`src/NaturalStoneImpex.Api/Services/IImageEditProvider.cs`:

```csharp
namespace NaturalStoneImpex.Api.Services;

public record ImageEditInput(
    byte[] CustomerPhoto,
    string CustomerPhotoMimeType,
    List<(byte[] Bytes, string MimeType)> ReferenceImages,
    string Prompt);

public record ImageEditResult(byte[] ImageBytes, string MimeType);

public class ImageEditProviderException : Exception
{
    public ImageEditProviderException(string message) : base(message) { }
    public ImageEditProviderException(string message, Exception inner) : base(message, inner) { }
}

public interface IImageEditProvider
{
    Task<ImageEditResult> EditAsync(ImageEditInput input, CancellationToken ct);
}
```

- [ ] **Step 4: Write the Gemini adapter**

`src/NaturalStoneImpex.Api/Services/GeminiImageEditProvider.cs`:

```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Models;

namespace NaturalStoneImpex.Api.Services;

public class GeminiImageEditProvider : IImageEditProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly GenerativeVisualizerOptions _options;

    public GeminiImageEditProvider(HttpClient http, IOptions<GenerativeVisualizerOptions> options)
    {
        _options = options.Value;
        _http = http;
        _http.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        _http.Timeout = TimeSpan.FromSeconds(_options.ProviderTimeoutSeconds);
        _http.DefaultRequestHeaders.Remove("x-goog-api-key");
        _http.DefaultRequestHeaders.Add("x-goog-api-key", _options.ApiKey);
    }

    public async Task<ImageEditResult> EditAsync(ImageEditInput input, CancellationToken ct)
    {
        var parts = new List<GeminiPart>
        {
            new("text", Text: input.Prompt),
            new("image", MimeType: input.CustomerPhotoMimeType,
                Data: Convert.ToBase64String(input.CustomerPhoto))
        };
        foreach (var (bytes, mime) in input.ReferenceImages)
            parts.Add(new GeminiPart("image", MimeType: mime, Data: Convert.ToBase64String(bytes)));

        var request = new GeminiRequest(_options.Model, parts, new GeminiResponseFormat("image"));
        var json = JsonSerializer.Serialize(request, JsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync("v1beta/interactions",
                new StringContent(json, Encoding.UTF8, "application/json"), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ImageEditProviderException("Gemini API request failed.", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new ImageEditProviderException(
                    $"Gemini API returned {(int)response.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("output_image", out var img) ||
                !img.TryGetProperty("data", out var data) ||
                data.GetString() is not { Length: > 0 } base64)
                throw new ImageEditProviderException("Gemini API response contained no output image.");

            var mimeType = img.TryGetProperty("mime_type", out var mt)
                ? mt.GetString() ?? "image/png"
                : "image/png";
            return new ImageEditResult(Convert.FromBase64String(base64), mimeType);
        }
    }

    private sealed record GeminiPart(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text = null,
        [property: JsonPropertyName("mime_type")] string? MimeType = null,
        [property: JsonPropertyName("data")] string? Data = null);

    private sealed record GeminiRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] List<GeminiPart> Input,
        [property: JsonPropertyName("response_format")] GeminiResponseFormat ResponseFormat);

    private sealed record GeminiResponseFormat(
        [property: JsonPropertyName("type")] string Type);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test src/NaturalStoneImpex.sln --filter GeminiImageEditProviderTests`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add src/NaturalStoneImpex.Api/Services tests/
git commit -m "feat: add image edit provider abstraction with Gemini adapter"
```

---

### Task 5: `VisualizerPhotoStore` — validate, re-encode, strip EXIF, token storage

**Files:**
- Create: `src/NaturalStoneImpex.Api/Services/VisualizerPhotoStore.cs`
- Test: `tests/NaturalStoneImpex.Api.Tests/VisualizerPhotoStoreTests.cs`

**Interfaces:**
- Consumes: `IWebHostEnvironment.WebRootPath`, `GenerativeVisualizerOptions` (`MaxUploadBytes`, `MaxImageDimension`).
- Produces (class `VisualizerPhotoStore`):
  - `Task<(string? Token, string? Error)> SavePhotoAsync(IFormFile photo, CancellationToken ct)`
  - `string? GetOriginalPath(string photoToken)` (null when missing/expired)
  - `string GetResultPath(string photoToken, int productId)`
  - `bool ResultExists(string photoToken, int productId)`
  - `Task SaveResultAsync(string photoToken, int productId, byte[] imageBytes, CancellationToken ct)` (re-encodes to JPEG)
  - `static bool IsValidToken(string token)`
  - `static string ResultUrl(string photoToken, int productId)` → `/uploads/visualizer/{token}/{productId}.jpg`

- [ ] **Step 1: Write the failing tests**

Create `tests/NaturalStoneImpex.Api.Tests/VisualizerPhotoStoreTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Models;
using NaturalStoneImpex.Api.Services;
using SixLabors.ImageSharp;

namespace NaturalStoneImpex.Api.Tests;

public class VisualizerPhotoStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nsi-tests", Guid.NewGuid().ToString("N"));
    private readonly VisualizerPhotoStore _store;

    public VisualizerPhotoStoreTests()
    {
        _store = new VisualizerPhotoStore(
            TestHelpers.CreateEnv(_root),
            Options.Create(new GenerativeVisualizerOptions { MaxImageDimension = 100 }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task SavePhotoAsync_ValidJpeg_StoresAndReturnsToken()
    {
        var file = TestHelpers.CreateFormFile(TestHelpers.CreateJpeg());

        var (token, error) = await _store.SavePhotoAsync(file, CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(token);
        Assert.True(VisualizerPhotoStore.IsValidToken(token!));
        Assert.NotNull(_store.GetOriginalPath(token!));
    }

    [Fact]
    public async Task SavePhotoAsync_OversizedImage_IsDownscaled()
    {
        var file = TestHelpers.CreateFormFile(TestHelpers.CreateJpeg(400, 200));

        var (token, _) = await _store.SavePhotoAsync(file, CancellationToken.None);

        using var image = await Image.LoadAsync(_store.GetOriginalPath(token!)!);
        Assert.Equal(100, image.Width);
        Assert.Null(image.Metadata.ExifProfile);
    }

    [Fact]
    public async Task SavePhotoAsync_NonImageFile_ReturnsError()
    {
        var file = TestHelpers.CreateFormFile(new byte[] { 0x4D, 0x5A, 0x90, 0x00 });

        var (token, error) = await _store.SavePhotoAsync(file, CancellationToken.None);

        Assert.Null(token);
        Assert.Equal("Моля, качете снимка във формат JPG или PNG до 10 MB.", error);
    }

    [Fact]
    public void IsValidToken_RejectsPathTraversal()
    {
        Assert.False(VisualizerPhotoStore.IsValidToken("..\\..\\evil"));
        Assert.False(VisualizerPhotoStore.IsValidToken("../etc"));
        Assert.False(VisualizerPhotoStore.IsValidToken(""));
        Assert.True(VisualizerPhotoStore.IsValidToken(Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public async Task SaveResultAsync_ResultExists_And_UrlIsCorrect()
    {
        var file = TestHelpers.CreateFormFile(TestHelpers.CreateJpeg());
        var (token, _) = await _store.SavePhotoAsync(file, CancellationToken.None);

        Assert.False(_store.ResultExists(token!, 5));
        await _store.SaveResultAsync(token!, 5, TestHelpers.CreateJpeg(), CancellationToken.None);

        Assert.True(_store.ResultExists(token!, 5));
        Assert.Equal($"/uploads/visualizer/{token}/5.jpg", VisualizerPhotoStore.ResultUrl(token!, 5));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/NaturalStoneImpex.sln --filter VisualizerPhotoStoreTests`
Expected: FAIL — `VisualizerPhotoStore` not defined.

- [ ] **Step 3: Write the implementation**

`src/NaturalStoneImpex.Api/Services/VisualizerPhotoStore.cs`:

```csharp
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace NaturalStoneImpex.Api.Services;

public class VisualizerPhotoStore
{
    private const string UploadError = "Моля, качете снимка във формат JPG или PNG до 10 MB.";

    private readonly string _rootDir;
    private readonly GenerativeVisualizerOptions _options;

    public VisualizerPhotoStore(IWebHostEnvironment env, IOptions<GenerativeVisualizerOptions> options)
    {
        _options = options.Value;
        _rootDir = Path.Combine(env.WebRootPath, "uploads", "visualizer");
    }

    public async Task<(string? Token, string? Error)> SavePhotoAsync(IFormFile photo, CancellationToken ct)
    {
        if (photo.Length == 0 || photo.Length > _options.MaxUploadBytes)
            return (null, UploadError);

        Image image;
        try
        {
            image = await Image.LoadAsync(photo.OpenReadStream(), ct);
        }
        catch (UnknownImageFormatException)
        {
            return (null, UploadError);
        }
        catch (InvalidImageContentException)
        {
            return (null, UploadError);
        }

        using (image)
        {
            if (image.Width > _options.MaxImageDimension || image.Height > _options.MaxImageDimension)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(_options.MaxImageDimension, _options.MaxImageDimension)
                }));
            }

            // Re-encoding to JPEG with cleared profiles removes EXIF/GPS metadata
            image.Metadata.ExifProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IptcProfile = null;

            var token = Guid.NewGuid().ToString("N");
            var dir = Path.Combine(_rootDir, token);
            Directory.CreateDirectory(dir);
            await image.SaveAsJpegAsync(Path.Combine(dir, "original.jpg"),
                new JpegEncoder { Quality = 85 }, ct);
            return (token, null);
        }
    }

    public string? GetOriginalPath(string photoToken)
    {
        if (!IsValidToken(photoToken)) return null;
        var path = Path.Combine(_rootDir, photoToken, "original.jpg");
        return File.Exists(path) ? path : null;
    }

    public string GetResultPath(string photoToken, int productId)
        => Path.Combine(_rootDir, photoToken, $"{productId}.jpg");

    public bool ResultExists(string photoToken, int productId)
        => IsValidToken(photoToken) && File.Exists(GetResultPath(photoToken, productId));

    public async Task SaveResultAsync(string photoToken, int productId, byte[] imageBytes, CancellationToken ct)
    {
        using var image = Image.Load(imageBytes);
        await image.SaveAsJpegAsync(GetResultPath(photoToken, productId),
            new JpegEncoder { Quality = 90 }, ct);
    }

    public static bool IsValidToken(string token)
        => token.Length == 32 && token.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static string ResultUrl(string photoToken, int productId)
        => $"/uploads/visualizer/{photoToken}/{productId}.jpg";
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/NaturalStoneImpex.sln --filter VisualizerPhotoStoreTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/NaturalStoneImpex.Api/Services tests/
git commit -m "feat: add visualizer photo store with content validation and EXIF stripping"
```

---

### Task 6: `GenerativeVisualizationService` — quotas, caching, orchestration

**Files:**
- Create: `src/NaturalStoneImpex.Api/Models/DTOs/VisualizerDtos.cs`
- Create: `src/NaturalStoneImpex.Api/Services/IGenerativeVisualizationService.cs`
- Create: `src/NaturalStoneImpex.Api/Services/GenerativeVisualizationService.cs`
- Test: `tests/NaturalStoneImpex.Api.Tests/GenerativeVisualizationServiceTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `VisualizerPhotoStore` (Task 5), `IImageEditProvider` (Task 4), `GenerativeVisualizerOptions`, `IWebHostEnvironment`.
- Produces:
  - `record GenerateVisualizationResponse(string PhotoToken, int ProductId, string ImageUrl, bool Cached)`
  - `interface IGenerativeVisualizationService { Task<(GenerateVisualizationResponse? Response, string? Error, int StatusCode)> GenerateAsync(IFormFile? photo, string? photoToken, int productId, bool consent, string clientIp, CancellationToken ct); }`
  - `static class VisualizerPromptBuilder { static string Build(string? productPromptHint); }`
  - `GenerativeVisualizationService.HashIp(string ip, string salt, DateTime utcNow)` (internal static, hex SHA-256)

- [ ] **Step 1: Write the failing tests**

Create `tests/NaturalStoneImpex.Api.Tests/GenerativeVisualizationServiceTests.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Tests;

public class GenerativeVisualizationServiceTests : IDisposable
{
    private sealed class FakeProvider : IImageEditProvider
    {
        public int Calls;
        public ImageEditInput? LastInput;
        public bool Throw;

        public Task<ImageEditResult> EditAsync(ImageEditInput input, CancellationToken ct)
        {
            Calls++;
            LastInput = input;
            if (Throw) throw new ImageEditProviderException("provider down");
            return Task.FromResult(new ImageEditResult(TestHelpers.CreateJpeg(), "image/png"));
        }
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "nsi-tests", Guid.NewGuid().ToString("N"));
    private readonly AppDbContext _db = TestHelpers.CreateDb();
    private readonly FakeProvider _provider = new();
    private readonly GenerativeVisualizerOptions _options = new() { Enabled = true, PerIpDailyLimit = 2, GlobalDailyLimit = 3 };
    private readonly GenerativeVisualizationService _service;

    public GenerativeVisualizationServiceTests()
    {
        var env = TestHelpers.CreateEnv(_root);
        var store = new VisualizerPhotoStore(env, Options.Create(_options));
        _service = new GenerativeVisualizationService(_db, store, _provider, Options.Create(_options), env);
        SeedProduct();
    }

    private void SeedProduct(int id = 1)
    {
        var texDir = Path.Combine(_root, "uploads", "products");
        Directory.CreateDirectory(texDir);
        File.WriteAllBytes(Path.Combine(texDir, $"tex{id}.jpg"), TestHelpers.CreateJpeg());
        _db.Categories.Add(new Category { Id = id, Name = $"Категория {id}" });
        _db.Products.Add(new Product
        {
            Id = id,
            Name = $"Гнайс {id}",
            CategoryId = id,
            IsActive = true,
            IsVisualizerEnabled = true,
            TextureImagePath = $"/uploads/products/tex{id}.jpg",
            VisualizerPromptHint = "grey gneiss slabs"
        });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private Task<(GenerateVisualizationResponse? Response, string? Error, int StatusCode)> GenerateWithPhotoAsync(
        string ip = "10.0.0.1", int productId = 1, bool consent = true)
        => _service.GenerateAsync(TestHelpers.CreateFormFile(TestHelpers.CreateJpeg()),
            photoToken: null, productId, consent, ip, CancellationToken.None);

    [Fact]
    public async Task Generate_FirstCall_StoresPhotoCallsProviderAndRecordsRequest()
    {
        var (response, error, status) = await GenerateWithPhotoAsync();

        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.False(response!.Cached);
        Assert.Equal($"/uploads/visualizer/{response.PhotoToken}/1.jpg", response.ImageUrl);
        Assert.Equal(1, _provider.Calls);
        Assert.Contains("grey gneiss slabs", _provider.LastInput!.Prompt);
        Assert.Single(_provider.LastInput.ReferenceImages);

        var row = Assert.Single(_db.VisualizationRequests);
        Assert.Equal(VisualizerMode.Generative, row.Mode);
        Assert.Equal(VisualizationStatus.Succeeded, row.Status);
        Assert.True(row.ConsentGiven);
        Assert.Equal(1, row.ProductId);
    }

    [Fact]
    public async Task Generate_SameTokenAndProduct_ReturnsCachedWithoutProviderCall()
    {
        var (first, _, _) = await GenerateWithPhotoAsync();

        var (second, error, status) = await _service.GenerateAsync(
            null, first!.PhotoToken, 1, consent: false, "10.0.0.1", CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.True(second!.Cached);
        Assert.Equal(1, _provider.Calls);
    }

    [Fact]
    public async Task Generate_PhotoWithoutConsent_Returns400()
    {
        var (response, error, status) = await GenerateWithPhotoAsync(consent: false);

        Assert.Null(response);
        Assert.Equal(400, status);
        Assert.Equal("Необходимо е съгласие за обработка на снимката.", error);
    }

    [Fact]
    public async Task Generate_UnknownToken_Returns404()
    {
        var (response, error, status) = await _service.GenerateAsync(
            null, new string('a', 32), 1, false, "10.0.0.1", CancellationToken.None);

        Assert.Null(response);
        Assert.Equal(404, status);
        Assert.Equal("Сесията е изтекла. Моля, качете снимката отново.", error);
    }

    [Fact]
    public async Task Generate_PerIpDailyLimit_Returns429()
    {
        SeedProduct(2);
        SeedProduct(3);
        await GenerateWithPhotoAsync(productId: 1);
        await GenerateWithPhotoAsync(productId: 2);

        var (_, error, status) = await GenerateWithPhotoAsync(productId: 3);

        Assert.Equal(429, status);
        Assert.Equal("Достигнахте дневния лимит за AI визуализации. Опитайте отново утре.", error);
    }

    [Fact]
    public async Task Generate_GlobalDailyLimit_Returns429()
    {
        SeedProduct(2);
        await GenerateWithPhotoAsync(ip: "10.0.0.1", productId: 1);
        await GenerateWithPhotoAsync(ip: "10.0.0.2", productId: 1);
        await GenerateWithPhotoAsync(ip: "10.0.0.3", productId: 2);

        var (_, error, status) = await GenerateWithPhotoAsync(ip: "10.0.0.4", productId: 2);

        Assert.Equal(429, status);
        Assert.Equal("AI режимът е временно недостъпен. Моля, опитайте по-късно.", error);
    }

    [Fact]
    public async Task Generate_ProviderFailure_Returns502AndRecordsFailedRow()
    {
        _provider.Throw = true;

        var (_, error, status) = await GenerateWithPhotoAsync();

        Assert.Equal(502, status);
        Assert.Equal("Визуализацията не можа да бъде генерирана. Моля, опитайте отново.", error);
        var row = Assert.Single(_db.VisualizationRequests);
        Assert.Equal(VisualizationStatus.Failed, row.Status);
    }

    [Fact]
    public async Task Generate_ProductNotEnabled_Returns404()
    {
        var product = _db.Products.Single(p => p.Id == 1);
        product.IsVisualizerEnabled = false;
        await _db.SaveChangesAsync();

        var (_, error, status) = await GenerateWithPhotoAsync();

        Assert.Equal(404, status);
        Assert.Equal("Продуктът не е наличен във визуализатора.", error);
    }

    [Fact]
    public async Task Generate_FlagDisabled_Returns404()
    {
        _options.Enabled = false;

        var (_, error, status) = await GenerateWithPhotoAsync();

        Assert.Equal(404, status);
        Assert.Equal("Визуализаторът не е достъпен.", error);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/NaturalStoneImpex.sln --filter GenerativeVisualizationServiceTests`
Expected: FAIL — compilation errors (service/DTO not defined).

- [ ] **Step 3: Write the DTO**

`src/NaturalStoneImpex.Api/Models/DTOs/VisualizerDtos.cs`:

```csharp
namespace NaturalStoneImpex.Api.Models.DTOs;

public record GenerateVisualizationResponse(
    string PhotoToken,
    int ProductId,
    string ImageUrl,
    bool Cached);

public class GenerateVisualizationForm
{
    public IFormFile? Photo { get; set; }
    public string? PhotoToken { get; set; }
    public int ProductId { get; set; }
    public bool Consent { get; set; }
}
```

- [ ] **Step 4: Write the service interface**

`src/NaturalStoneImpex.Api/Services/IGenerativeVisualizationService.cs`:

```csharp
using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Services;

public interface IGenerativeVisualizationService
{
    Task<(GenerateVisualizationResponse? Response, string? Error, int StatusCode)> GenerateAsync(
        IFormFile? photo, string? photoToken, int productId, bool consent,
        string clientIp, CancellationToken ct);
}
```

- [ ] **Step 5: Write the service implementation**

`src/NaturalStoneImpex.Api/Services/GenerativeVisualizationService.cs`:

```csharp
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Services;

public static class VisualizerPromptBuilder
{
    public static string Build(string? productPromptHint)
    {
        var prompt =
            "The first image is a customer's photo of their outdoor property. " +
            "The following image(s) show a paving stone product: first its texture, then optionally the product installed. " +
            "Replace the ground surface (driveway, path or yard area) in the first image with this paving stone. " +
            "Preserve the camera perspective, lighting, shadows, and every other object in the scene - buildings, cars, plants, people. " +
            "Lay the paving at a realistic scale with visible joints. ";
        if (!string.IsNullOrWhiteSpace(productPromptHint))
            prompt += $"Product description: {productPromptHint.Trim()}. ";
        return prompt + "Return only the edited photograph.";
    }
}

public class GenerativeVisualizationService : IGenerativeVisualizationService
{
    private readonly AppDbContext _context;
    private readonly VisualizerPhotoStore _photoStore;
    private readonly IImageEditProvider _provider;
    private readonly GenerativeVisualizerOptions _options;
    private readonly IWebHostEnvironment _env;

    public GenerativeVisualizationService(
        AppDbContext context,
        VisualizerPhotoStore photoStore,
        IImageEditProvider provider,
        IOptions<GenerativeVisualizerOptions> options,
        IWebHostEnvironment env)
    {
        _context = context;
        _photoStore = photoStore;
        _provider = provider;
        _options = options.Value;
        _env = env;
    }

    public async Task<(GenerateVisualizationResponse? Response, string? Error, int StatusCode)> GenerateAsync(
        IFormFile? photo, string? photoToken, int productId, bool consent,
        string clientIp, CancellationToken ct)
    {
        if (!_options.Enabled)
            return (null, "Визуализаторът не е достъпен.", 404);

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive && p.IsVisualizerEnabled, ct);
        if (product is null)
            return (null, "Продуктът не е наличен във визуализатора.", 404);

        if (photo is not null)
        {
            if (!consent)
                return (null, "Необходимо е съгласие за обработка на снимката.", 400);

            var (token, uploadError) = await _photoStore.SavePhotoAsync(photo, ct);
            if (uploadError is not null)
                return (null, uploadError, 400);
            photoToken = token;
        }
        else if (string.IsNullOrEmpty(photoToken) || _photoStore.GetOriginalPath(photoToken) is null)
        {
            return (null, "Сесията е изтекла. Моля, качете снимката отново.", 404);
        }

        if (_photoStore.ResultExists(photoToken!, productId))
            return (new GenerateVisualizationResponse(photoToken!, productId,
                VisualizerPhotoStore.ResultUrl(photoToken!, productId), Cached: true), null, 200);

        var today = DateTime.UtcNow.Date;
        var ipHash = HashIp(clientIp, _options.IpHashSalt, DateTime.UtcNow);

        var ipCount = await _context.VisualizationRequests.CountAsync(
            r => r.Mode == VisualizerMode.Generative && r.IpHash == ipHash && r.CreatedAt >= today, ct);
        if (ipCount >= _options.PerIpDailyLimit)
            return (null, "Достигнахте дневния лимит за AI визуализации. Опитайте отново утре.", 429);

        var globalCount = await _context.VisualizationRequests.CountAsync(
            r => r.Mode == VisualizerMode.Generative && r.CreatedAt >= today, ct);
        if (globalCount >= _options.GlobalDailyLimit)
            return (null, "AI режимът е временно недостъпен. Моля, опитайте по-късно.", 429);

        var originalPath = _photoStore.GetOriginalPath(photoToken!)!;
        var customerPhoto = await File.ReadAllBytesAsync(originalPath, ct);
        var references = await LoadReferenceImagesAsync(product, ct);
        if (references.Count == 0)
            return (null, "Продуктът няма изображение за визуализация.", 400);

        var input = new ImageEditInput(customerPhoto, "image/jpeg", references,
            VisualizerPromptBuilder.Build(product.VisualizerPromptHint));

        var stopwatch = Stopwatch.StartNew();
        var request = new VisualizationRequest
        {
            Mode = VisualizerMode.Generative,
            IpHash = ipHash,
            PhotoToken = photoToken,
            ProductId = productId,
            ConsentGiven = consent || photo is null,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            var result = await _provider.EditAsync(input, ct);
            await _photoStore.SaveResultAsync(photoToken!, productId, result.ImageBytes, ct);

            request.Status = VisualizationStatus.Succeeded;
            request.ProviderCostEstimate = _options.EstimatedCostPerImage;
            request.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            _context.VisualizationRequests.Add(request);
            await _context.SaveChangesAsync(ct);

            return (new GenerateVisualizationResponse(photoToken!, productId,
                VisualizerPhotoStore.ResultUrl(photoToken!, productId), Cached: false), null, 200);
        }
        catch (ImageEditProviderException)
        {
            request.Status = VisualizationStatus.Failed;
            request.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            _context.VisualizationRequests.Add(request);
            await _context.SaveChangesAsync(ct);

            return (null, "Визуализацията не можа да бъде генерирана. Моля, опитайте отново.", 502);
        }
    }

    private async Task<List<(byte[] Bytes, string MimeType)>> LoadReferenceImagesAsync(
        Product product, CancellationToken ct)
    {
        var references = new List<(byte[], string)>();
        foreach (var relativePath in new[] { product.TextureImagePath, product.ImagePath })
        {
            if (string.IsNullOrEmpty(relativePath)) continue;
            var fullPath = Path.Combine(_env.WebRootPath,
                relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath)) continue;
            var mime = fullPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png" : "image/jpeg";
            references.Add((await File.ReadAllBytesAsync(fullPath, ct), mime));
        }
        return references;
    }

    internal static string HashIp(string ip, string salt, DateTime utcNow)
    {
        var input = $"{ip}:{utcNow:yyyyMMdd}:{salt}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test src/NaturalStoneImpex.sln --filter GenerativeVisualizationServiceTests`
Expected: PASS (9 tests).

- [ ] **Step 7: Commit**

```bash
git add src/NaturalStoneImpex.Api tests/
git commit -m "feat: add generative visualization service with quotas and result caching"
```

---

### Task 7: Controller endpoint + rate limiting + DI wiring

**Files:**
- Modify (or Create per Prerequisite Contract #3): `src/NaturalStoneImpex.Api/Controllers/VisualizerController.cs`
- Modify: `src/NaturalStoneImpex.Api/Program.cs`

**Interfaces:**
- Consumes: `IGenerativeVisualizationService` (Task 6), `GenerateVisualizationForm` (Task 6), `GenerativeVisualizerOptions`.
- Produces: `POST /api/visualizer/generate` (multipart, anonymous, rate-limit policy `"visualizer-burst"`).

- [ ] **Step 1: Add the endpoint to `VisualizerController`**

If the controller exists from Option 2, add the constructor dependency + action; otherwise create:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Controllers;

[ApiController]
[Route("api/visualizer")]
public class VisualizerController : ControllerBase
{
    private readonly IGenerativeVisualizationService _generativeService;

    public VisualizerController(IGenerativeVisualizationService generativeService)
    {
        _generativeService = generativeService;
    }

    [HttpPost("generate")]
    [EnableRateLimiting("visualizer-burst")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Generate([FromForm] GenerateVisualizationForm form, CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (response, error, statusCode) = await _generativeService.GenerateAsync(
            form.Photo, form.PhotoToken, form.ProductId, form.Consent, clientIp, ct);

        if (error is not null)
            return StatusCode(statusCode, new { error });

        return Ok(response);
    }
}
```

(When merging into an existing Option 2 controller: keep its existing constructor parameters and actions; add `_generativeService` as an additional dependency.)

- [ ] **Step 2: Wire DI + rate limiter in `Program.cs`**

Add to the usings:

```csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
```

After the `builder.Services.Configure<GenerativeVisualizerOptions>(...)` line (Task 3) add:

```csharp
builder.Services.AddSingleton<VisualizerPhotoStore>();
builder.Services.AddHttpClient<GeminiImageEditProvider>();
builder.Services.AddScoped<IImageEditProvider>(sp => sp.GetRequiredService<GeminiImageEditProvider>());
builder.Services.AddScoped<IGenerativeVisualizationService, GenerativeVisualizationService>();

var generativeOptions = builder.Configuration
    .GetSection(GenerativeVisualizerOptions.SectionName)
    .Get<GenerativeVisualizerOptions>() ?? new GenerativeVisualizerOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\":\"Твърде много заявки. Моля, изчакайте минута.\"}", ct);
    };
    options.AddPolicy("visualizer-burst", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = generativeOptions.BurstPerMinute,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

In the middleware pipeline, after `app.UseCors("BlazorClient");` add:

```csharp
app.UseRateLimiter();
```

- [ ] **Step 3: Verify build and full test suite**

Run: `dotnet build src/NaturalStoneImpex.sln`, then `dotnet test src/NaturalStoneImpex.sln`
Expected: build succeeds; all tests pass.

- [ ] **Step 4: Manual smoke test (flag on, no real key needed for the error path)**

Run: `dotnet run --project src/NaturalStoneImpex.Api` and in a second terminal:

```bash
curl -k -s -o /dev/null -w "%{http_code}" -X POST https://localhost:5001/api/visualizer/generate \
  -F "productId=1" -F "consent=true"
```

Expected: `404` with the disabled message (flag is off by default) — confirms routing, form binding, and the config gate. Stop the API.

- [ ] **Step 5: Commit**

```bash
git add src/NaturalStoneImpex.Api
git commit -m "feat: add generate endpoint with per-IP burst rate limiting"
```

---

### Task 8: `VisualizerCleanupService` — 48 h photo retention

**Files:**
- Create: `src/NaturalStoneImpex.Api/Services/VisualizerCleanupService.cs`
- Modify: `src/NaturalStoneImpex.Api/Program.cs`
- Test: `tests/NaturalStoneImpex.Api.Tests/VisualizerCleanupServiceTests.cs`

**Interfaces:**
- Consumes: `IServiceScopeFactory` (for `AppDbContext`), `IWebHostEnvironment`, `GenerativeVisualizerOptions.RetentionHours`.
- Produces: `VisualizerCleanupService : BackgroundService` with public `Task CleanupOnceAsync(CancellationToken ct)` (hourly loop calls it; tests call it directly).

- [ ] **Step 1: Write the failing tests**

Create `tests/NaturalStoneImpex.Api.Tests/VisualizerCleanupServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Tests;

public class VisualizerCleanupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nsi-tests", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _serviceProvider;
    private readonly VisualizerCleanupService _service;

    public VisualizerCleanupServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        _serviceProvider = services.BuildServiceProvider();

        _service = new VisualizerCleanupService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            TestHelpers.CreateEnv(_root),
            Options.Create(new GenerativeVisualizerOptions { RetentionHours = 48 }),
            NullLogger<VisualizerCleanupService>.Instance);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string CreateTokenDir(string name, DateTime lastWriteUtc)
    {
        var dir = Path.Combine(_root, "uploads", "visualizer", name);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "original.jpg"), TestHelpers.CreateJpeg());
        Directory.SetLastWriteTimeUtc(dir, lastWriteUtc);
        return dir;
    }

    [Fact]
    public async Task CleanupOnce_DeletesExpiredFolders_KeepsFreshOnes()
    {
        var expired = CreateTokenDir("aaaa", DateTime.UtcNow.AddHours(-50));
        var fresh = CreateTokenDir("bbbb", DateTime.UtcNow.AddHours(-1));

        await _service.CleanupOnceAsync(CancellationToken.None);

        Assert.False(Directory.Exists(expired));
        Assert.True(Directory.Exists(fresh));
    }

    [Fact]
    public async Task CleanupOnce_PrunesRequestRowsOlderThan90Days()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.VisualizationRequests.Add(new VisualizationRequest
            { IpHash = "old", CreatedAt = DateTime.UtcNow.AddDays(-91) });
            db.VisualizationRequests.Add(new VisualizationRequest
            { IpHash = "new", CreatedAt = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
        }

        await _service.CleanupOnceAsync(CancellationToken.None);

        using var verifyScope = _serviceProvider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var remaining = Assert.Single(verifyDb.VisualizationRequests);
        Assert.Equal("new", remaining.IpHash);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/NaturalStoneImpex.sln --filter VisualizerCleanupServiceTests`
Expected: FAIL — `VisualizerCleanupService` not defined.

- [ ] **Step 3: Write the implementation**

`src/NaturalStoneImpex.Api/Services/VisualizerCleanupService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models;

namespace NaturalStoneImpex.Api.Services;

public class VisualizerCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _env;
    private readonly IOptions<GenerativeVisualizerOptions> _options;
    private readonly ILogger<VisualizerCleanupService> _logger;

    public VisualizerCleanupService(
        IServiceScopeFactory scopeFactory,
        IWebHostEnvironment env,
        IOptions<GenerativeVisualizerOptions> options,
        ILogger<VisualizerCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _env = env;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Visualizer cleanup run failed.");
            }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    public async Task CleanupOnceAsync(CancellationToken ct)
    {
        var rootDir = Path.Combine(_env.WebRootPath, "uploads", "visualizer");
        if (Directory.Exists(rootDir))
        {
            var cutoff = DateTime.UtcNow.AddHours(-_options.Value.RetentionHours);
            foreach (var dir in Directory.GetDirectories(rootDir))
            {
                if (Directory.GetLastWriteTimeUtc(dir) < cutoff)
                    Directory.Delete(dir, recursive: true);
            }
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rowCutoff = DateTime.UtcNow.AddDays(-90);
        var oldRows = await db.VisualizationRequests
            .Where(r => r.CreatedAt < rowCutoff)
            .ToListAsync(ct);
        db.VisualizationRequests.RemoveRange(oldRows);
        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Register in `Program.cs`**

After `builder.Services.AddScoped<IGenerativeVisualizationService, GenerativeVisualizationService>();` add:

```csharp
builder.Services.AddHostedService<VisualizerCleanupService>();
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test src/NaturalStoneImpex.sln --filter VisualizerCleanupServiceTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add src/NaturalStoneImpex.Api tests/
git commit -m "feat: add 48h retention cleanup service for visualizer photos"
```

---

### Task 9: Admin API — `VisualizerPromptHint` through product DTOs and service

**Files:**
- Modify: `src/NaturalStoneImpex.Api/Models/DTOs/` — the files containing `CreateProductRequest`, `UpdateProductRequest`, `ProductDto` (locate with `grep -r "class CreateProductRequest" src/`)
- Modify: `src/NaturalStoneImpex.Api/Services/ProductService.cs`
- Test: `tests/NaturalStoneImpex.Api.Tests/ProductServicePromptHintTests.cs`

**Interfaces:**
- Consumes: existing `IProductService.CreateAsync/UpdateAsync/GetByIdAsync`.
- Produces: `CreateProductRequest.VisualizerPromptHint` (string?), `UpdateProductRequest.VisualizerPromptHint` (string?), `ProductDto.VisualizerPromptHint` (string?) — persisted through create/update, returned by get-by-id.

- [ ] **Step 1: Write the failing test**

Create `tests/NaturalStoneImpex.Api.Tests/ProductServicePromptHintTests.cs`:

```csharp
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Tests;

public class ProductServicePromptHintTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nsi-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task CreateAndUpdate_PersistVisualizerPromptHint()
    {
        using var db = TestHelpers.CreateDb();
        db.Categories.Add(new Category { Id = 1, Name = "Гнайс" });
        await db.SaveChangesAsync();
        var service = new ProductService(db, TestHelpers.CreateEnv(_root));

        var created = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Гнайс сив",
            CategoryId = 1,
            PriceWithoutVat = 20m,
            VatAmount = 4m,
            PriceWithVat = 24m,
            Unit = 1,
            StockQuantity = 100m,
            VisualizerPromptHint = "grey gneiss slabs"
        });
        Assert.Equal("grey gneiss slabs", created.VisualizerPromptHint);

        var updated = await service.UpdateAsync(created.Id, new UpdateProductRequest
        {
            Name = "Гнайс сив",
            CategoryId = 1,
            PriceWithoutVat = 20m,
            VatAmount = 4m,
            PriceWithVat = 24m,
            Unit = 1,
            StockQuantity = 100m,
            VisualizerPromptHint = "beige gneiss, wide joints"
        });
        Assert.Equal("beige gneiss, wide joints", updated!.VisualizerPromptHint);

        var fetched = await service.GetByIdAsync(created.Id);
        Assert.Equal("beige gneiss, wide joints", fetched!.VisualizerPromptHint);
    }
}
```

Note: if `CreateProductRequest`/`UpdateProductRequest` use constructor parameters (records) instead of object initializers, adapt the test construction style to match — the assertion targets stay the same.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/NaturalStoneImpex.sln --filter ProductServicePromptHintTests`
Expected: FAIL — `VisualizerPromptHint` not a member.

- [ ] **Step 3: Add the property to the three DTOs**

In the DTO definitions of `CreateProductRequest`, `UpdateProductRequest`, and `ProductDto`, add (matching each file's property style):

```csharp
    public string? VisualizerPromptHint { get; set; }
```

- [ ] **Step 4: Map it in `ProductService`**

In `CreateAsync`, inside the `new Product { ... }` initializer add:

```csharp
            VisualizerPromptHint = request.VisualizerPromptHint,
```

In `CreateAsync`'s returned `new ProductDto { ... }`, in `UpdateAsync`'s returned `new ProductDto { ... }`, and in `GetByIdAsync`'s `Select(... new ProductDto { ... })` add:

```csharp
            VisualizerPromptHint = product.VisualizerPromptHint,
```

(In `GetByIdAsync` the source is the query variable `p`: `VisualizerPromptHint = p.VisualizerPromptHint,`.)

In `UpdateAsync`, with the other `product.X = request.X;` assignments add:

```csharp
        product.VisualizerPromptHint = request.VisualizerPromptHint;
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test src/NaturalStoneImpex.sln --filter ProductServicePromptHintTests`
Expected: PASS (1 test).

- [ ] **Step 6: Commit**

```bash
git add src/NaturalStoneImpex.Api tests/
git commit -m "feat: expose visualizer prompt hint through product admin API"
```

---

### Task 10: Client — models + `GenerateAsync` in the visualizer service

**Files:**
- Create or Modify: `src/NaturalStoneImpex.Client/Models/VisualizerModels.cs`
- Modify (or Create per Prerequisite Contract #4): `src/NaturalStoneImpex.Client/Services/IVisualizerService.cs`, `src/NaturalStoneImpex.Client/Services/VisualizerService.cs`
- Modify: `src/NaturalStoneImpex.Client/Program.cs`

**Interfaces:**
- Consumes: shared `HttpClient` (named `"NaturalStoneImpex.Api"`, already registered).
- Produces:
  - `class GenerateVisualizationResponse { string PhotoToken; int ProductId; string ImageUrl; bool Cached; }` (client model, mutable for URL rewriting)
  - `IVisualizerService.GenerateAsync(Stream? photoStream, string? fileName, string? photoToken, int productId, bool consent)` → `Task<(GenerateVisualizationResponse? Result, string? Error)>`

- [ ] **Step 1: Add the client model**

`src/NaturalStoneImpex.Client/Models/VisualizerModels.cs` (append if the file exists from Option 2):

```csharp
namespace NaturalStoneImpex.Client.Models;

public class GenerateVisualizationResponse
{
    public string PhotoToken { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool Cached { get; set; }
}
```

- [ ] **Step 2: Add the service method**

Add to `IVisualizerService` (create the interface if Option 2 hasn't):

```csharp
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public interface IVisualizerService
{
    Task<(GenerateVisualizationResponse? Result, string? Error)> GenerateAsync(
        Stream? photoStream, string? fileName, string? photoToken, int productId, bool consent);
}
```

Implementation in `VisualizerService` (create the class with this method if Option 2 hasn't; otherwise add the method, reusing the class's existing `_httpClient`/`_apiBaseUrl` fields which follow the `ProductService` pattern):

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public class VisualizerService : IVisualizerService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public VisualizerService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiBaseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "";
    }

    public async Task<(GenerateVisualizationResponse? Result, string? Error)> GenerateAsync(
        Stream? photoStream, string? fileName, string? photoToken, int productId, bool consent)
    {
        using var content = new MultipartFormDataContent();
        if (photoStream is not null)
        {
            var streamContent = new StreamContent(photoStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(streamContent, "photo", fileName ?? "photo.jpg");
        }
        if (!string.IsNullOrEmpty(photoToken))
            content.Add(new StringContent(photoToken), "photoToken");
        content.Add(new StringContent(productId.ToString()), "productId");
        content.Add(new StringContent(consent ? "true" : "false"), "consent");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("api/visualizer/generate", content);
        }
        catch (HttpRequestException)
        {
            return (null, "Възникна грешка при връзката със сървъра.");
        }

        if (!response.IsSuccessStatusCode)
            return (null, await ExtractErrorAsync(response));

        var result = await response.Content.ReadFromJsonAsync<GenerateVisualizationResponse>();
        if (result is null)
            return (null, "Възникна неочаквана грешка.");
        result.ImageUrl = $"{_apiBaseUrl}{result.ImageUrl}";
        return (result, null);
    }

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

- [ ] **Step 3: Register in the client `Program.cs` (if not already registered by Option 2)**

With the other `AddScoped` service registrations:

```csharp
builder.Services.AddScoped<IVisualizerService, VisualizerService>();
```

- [ ] **Step 4: Verify build**

Run: `dotnet build src/NaturalStoneImpex.sln`
Expected: success.

- [ ] **Step 5: Commit**

```bash
git add src/NaturalStoneImpex.Client
git commit -m "feat: add client service call for generative visualization"
```

---

### Task 11: Client UI — `GenerativeVisualizerPanel` + mode toggle on the visualizer page

**Files:**
- Create: `src/NaturalStoneImpex.Client/Components/GenerativeVisualizerPanel.razor`
- Modify: `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor` (from Option 2; see Prerequisite Contract #4)

**Interfaces:**
- Consumes: `IVisualizerService.GenerateAsync` (Task 10); page-provided photo state.
- Produces: component `<GenerativeVisualizerPanel PhotoBytes="byte[]?" PhotoFileName="string" OriginalImageDataUrl="string?" SelectedProductId="int" SelectedProductName="string" />` — fully self-contained generative flow (consent, generate, progress, result, before/after toggle, AI badge, download).

- [ ] **Step 1: Create the panel component**

`src/NaturalStoneImpex.Client/Components/GenerativeVisualizerPanel.razor`:

```razor
@using NaturalStoneImpex.Client.Models
@using NaturalStoneImpex.Client.Services
@inject IVisualizerService VisualizerService

<div class="card">
    <div class="card-body">
        @if (!_consentGiven)
        {
            <div class="form-check mb-3">
                <input class="form-check-input" type="checkbox" id="aiConsent" @bind="_consentChecked" />
                <label class="form-check-label" for="aiConsent">
                    Съгласен/на съм снимката ми да бъде изпратена за обработка към външна AI услуга (Google)
                    с цел генериране на визуализация. Снимката и резултатите се изтриват автоматично до 48 часа.
                </label>
            </div>
        }

        @if (_error is not null)
        {
            <div class="alert alert-danger" role="alert">@_error</div>
        }

        @if (_generating)
        {
            <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Зареждане...</span>
                </div>
                <p class="mt-3">Генерираме фотореалистична визуализация… Обикновено отнема 5–20 секунди.</p>
            </div>
        }
        else if (CurrentResultUrl is not null)
        {
            <div class="position-relative">
                <img src="@(_showOriginal ? OriginalImageDataUrl : CurrentResultUrl)"
                     class="img-fluid rounded" alt="Визуализация" />
                <span class="badge text-bg-dark position-absolute bottom-0 end-0 m-2">
                    Генерирано с изкуствен интелект
                </span>
            </div>
            <div class="btn-group mt-2" role="group">
                <button type="button" class="btn btn-sm @(_showOriginal ? "btn-primary" : "btn-outline-primary")"
                        @onclick="() => _showOriginal = true">Преди</button>
                <button type="button" class="btn btn-sm @(!_showOriginal ? "btn-primary" : "btn-outline-primary")"
                        @onclick="() => _showOriginal = false">След</button>
            </div>
            <a class="btn btn-sm btn-outline-secondary mt-2 ms-2" href="@CurrentResultUrl"
               download="vizualizacia.jpg" target="_blank">Изтегли изображението</a>
            <p class="text-muted small mt-2 mb-0">
                Визуализацията е ориентировъчна. Реалният продукт може да се различава по цвят и вид.
            </p>
        }

        @if (!_generating)
        {
            <button type="button" class="btn btn-primary mt-3"
                    disabled="@(!CanGenerate)" @onclick="GenerateAsync">
                Генерирай визуализация
            </button>
        }
    </div>
</div>

@code {
    [Parameter] public byte[]? PhotoBytes { get; set; }
    [Parameter] public string PhotoFileName { get; set; } = "photo.jpg";
    [Parameter] public string? OriginalImageDataUrl { get; set; }
    [Parameter] public int SelectedProductId { get; set; }
    [Parameter] public string SelectedProductName { get; set; } = string.Empty;

    private readonly Dictionary<int, string> _resultsByProduct = new();
    private string? _photoToken;
    private bool _consentChecked;
    private bool _consentGiven;
    private bool _generating;
    private bool _showOriginal;
    private string? _error;
    private int _lastGeneratedProductId;

    private string? CurrentResultUrl =>
        _resultsByProduct.TryGetValue(SelectedProductId, out var url) ? url : null;

    private bool CanGenerate =>
        PhotoBytes is not null && SelectedProductId > 0 &&
        (_consentGiven || _consentChecked) && CurrentResultUrl is null;

    protected override async Task OnParametersSetAsync()
    {
        // Auto-generate on product switch once the first generation happened
        if (_photoToken is not null && SelectedProductId > 0 &&
            SelectedProductId != _lastGeneratedProductId &&
            !_generating && CurrentResultUrl is null)
        {
            await GenerateAsync();
        }
    }

    private async Task GenerateAsync()
    {
        if (PhotoBytes is null || SelectedProductId <= 0) return;
        _error = null;
        _generating = true;
        _showOriginal = false;

        Stream? photoStream = _photoToken is null ? new MemoryStream(PhotoBytes) : null;
        var (result, error) = await VisualizerService.GenerateAsync(
            photoStream, PhotoFileName, _photoToken, SelectedProductId, _consentChecked || _consentGiven);
        photoStream?.Dispose();

        _generating = false;
        if (error is not null)
        {
            _error = error;
            if (error.Contains("Сесията е изтекла"))
                _photoToken = null;
            return;
        }

        _consentGiven = true;
        _photoToken = result!.PhotoToken;
        _lastGeneratedProductId = result.ProductId;
        _resultsByProduct[result.ProductId] = result.ImageUrl;
    }

    /// Reset when the page loads a new photo.
    public void ResetForNewPhoto()
    {
        _photoToken = null;
        _resultsByProduct.Clear();
        _error = null;
        _showOriginal = false;
        _lastGeneratedProductId = 0;
        StateHasChanged();
    }
}
```

- [ ] **Step 2: Integrate into `Visualizer.razor` (mode toggle)**

In the Option 2 page, add mode state and the toggle above the canvas area. The exact field names for the photo state come from the Option 2 implementation — bind them where indicated:

```razor
@* near the top of the page content, after the photo is uploaded *@
<div class="btn-group mb-3" role="group" aria-label="Режим">
    <button type="button" class="btn @(_mode == VisualizerMode.Exact ? "btn-primary" : "btn-outline-primary")"
            @onclick="() => _mode = VisualizerMode.Exact">Точна текстура</button>
    <button type="button" class="btn @(_mode == VisualizerMode.Generative ? "btn-primary" : "btn-outline-primary")"
            @onclick="() => _mode = VisualizerMode.Generative">Фотореалистичен (AI)</button>
</div>

@if (_mode == VisualizerMode.Generative)
{
    <GenerativeVisualizerPanel @ref="_generativePanel"
                               PhotoBytes="@/* page's uploaded photo bytes field */"
                               PhotoFileName="@/* page's uploaded file name field */"
                               OriginalImageDataUrl="@/* page's original photo data URL field */"
                               SelectedProductId="@/* page's selected product id field */"
                               SelectedProductName="@/* page's selected product name field */" />
}
else
{
    @* existing Option 2 canvas/renderer markup stays here unchanged *@
}

@code {
    private enum VisualizerMode { Exact, Generative }
    private VisualizerMode _mode = VisualizerMode.Exact;
    private GenerativeVisualizerPanel? _generativePanel;
    // In the page's existing "new photo uploaded" handler, add: _generativePanel?.ResetForNewPhoto();
}
```

The five `/* … */` placeholders are deliberate integration points — they map to the Option 2 page's existing state fields, whose names are fixed only once that page exists. Everything else in this task is complete as written. Hide the toggle entirely if the products response indicates generative mode is disabled (simplest V1: attempt generation only when the server flag is on; the 404 error message renders in the panel otherwise).

- [ ] **Step 3: Verify build**

Run: `dotnet build src/NaturalStoneImpex.sln`
Expected: success.

- [ ] **Step 4: Manual verification (requires a real API key and `Enabled: true` locally)**

1. `dotnet user-secrets set "Visualizer:Generative:ApiKey" "<real key>" --project src/NaturalStoneImpex.Api` and set `"Enabled": true` in the local config (do not commit).
2. Run API + Client (`dotnet run --project src/NaturalStoneImpex.Api`, `dotnet run --project src/NaturalStoneImpex.Client`).
3. On `/visualizer`: upload a driveway photo → switch to «Фотореалистичен (AI)» → check consent → «Генерирай» → result appears with AI badge in ≤ 20 s.
4. Switch product in the panel → regeneration runs; switch back → instant (cached).
5. «Преди»/«След» toggles; download works.
6. Revert local `Enabled` to `false`.

- [ ] **Step 5: Commit**

```bash
git add src/NaturalStoneImpex.Client
git commit -m "feat: add generative AI mode panel to visualizer page"
```

---

### Task 12: Admin UI — prompt hint field in the product form

**Files:**
- Modify: `src/NaturalStoneImpex.Client/Pages/Admin/ProductForm.razor`
- Modify: client model files containing `CreateProductRequest`, `UpdateProductRequest`, `ProductDto` (locate with `grep -r "class CreateProductRequest" src/NaturalStoneImpex.Client/`)

**Interfaces:**
- Consumes: Task 9's API DTO fields (same property name `VisualizerPromptHint`).
- Produces: admin can view/edit the hint; round-trips through the existing product create/update calls.

- [ ] **Step 1: Add `VisualizerPromptHint` to the client DTOs**

In the client's `CreateProductRequest`, `UpdateProductRequest`, and `ProductDto` classes add:

```csharp
    public string? VisualizerPromptHint { get; set; }
```

- [ ] **Step 2: Add the form field**

In `ProductForm.razor`, after the description field (and after Option 2's visualizer fields if present), add — where `@bind-Value` targets the page's form model variable (the same one the description field binds to):

```razor
<div class="mb-3">
    <label class="form-label">AI описание <span class="text-muted">(на английски — за фотореалистичния режим, незадължително)</span></label>
    <InputTextArea class="form-control" rows="2" @bind-Value="_model.VisualizerPromptHint"
                   placeholder="напр. irregular grey-beige gneiss slabs with wide joints" />
</div>
```

(Adjust `_model` to the page's actual form model field name; ensure the page maps the value into both create and update requests where the other fields are mapped.)

- [ ] **Step 3: Verify build + manual check**

Run: `dotnet build src/NaturalStoneImpex.sln` → success. Run API + Client, log in as admin (`admin` / `Admin123!`), edit a product, set the hint, save, reopen — the value persists.

- [ ] **Step 4: Commit**

```bash
git add src/NaturalStoneImpex.Client
git commit -m "feat: add AI prompt hint field to admin product form"
```

---

### Task 13: Documentation + final verification

**Files:**
- Modify: `docs/api-endpoints.md`
- Modify: `docs/database-schema.md`

**Interfaces:** none (docs + verification only).

- [ ] **Step 1: Document the endpoint in `docs/api-endpoints.md`**

Append under a `## Visualizer` section (create it if Option 2 hasn't):

```markdown
### POST /api/visualizer/generate

Generates a photorealistic AI visualization (generative mode). Anonymous. Rate limited (burst per IP + daily quotas). Multipart form data:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| photo | file (JPG/PNG/WebP ≤ 10 MB) | First call | Re-encoded server-side; EXIF stripped |
| photoToken | string | Product switches | 32-char token from a previous response |
| productId | int | Yes | Must be visualizer-enabled |
| consent | bool | With photo | Third-party processing consent |

**200**: `{ "photoToken": "…", "productId": 5, "imageUrl": "/uploads/visualizer/…/5.jpg", "cached": false }`
**Errors**: `{ "error": "…" }` — 400 (validation/consent), 404 (disabled/expired token/product), 429 (quota), 502 (AI provider failure).
```

- [ ] **Step 2: Document the schema changes in `docs/database-schema.md`**

Append to the `Product` column list: `VisualizerPromptHint nvarchar(500) NULL — English hint injected into the AI prompt (generative visualizer mode).`

Append to (or create) the `VisualizationRequest` table description the generative columns: `Mode int (0 = Segmentation, 1 = Generative)`, `PhotoToken nvarchar(64) NULL`, `ProductId int NULL FK → Product`, `ProviderCostEstimate decimal(18,4) NULL`, `ConsentGiven bit`.

- [ ] **Step 3: Full verification pass**

```bash
dotnet build src/NaturalStoneImpex.sln
dotnet test src/NaturalStoneImpex.sln
```

Expected: build clean; all tests pass (≥ 21 across Tasks 2–9). Then confirm the feature ships dark: run the API with committed config and `curl -k -X POST https://localhost:5001/api/visualizer/generate -F "productId=1" -F "consent=true"` → 404 `{"error":"Визуализаторът не е достъпен."}`. Confirm `git grep -i "Visualizer__Generative__ApiKey\|ApiKey.*AIza"` finds no committed secrets.

- [ ] **Step 4: Commit**

```bash
git add docs/
git commit -m "docs: document generative visualizer endpoint and schema changes"
```

---

## Self-Review Notes (kept for the executor)

- **Spec coverage**: §3 flow → Tasks 10–11; §5.1–5.2 provider → Task 4; §5.3 prompt → Task 6 (`VisualizerPromptBuilder`); §5.4 storage/capability URLs → Task 5 + static files (already enabled via `app.UseStaticFiles()`); §5.5 quotas/burst → Tasks 6–7; §6.1 config → Task 3; §7 data model → Task 2; §8 admin → Tasks 9 + 12; §10.6 EXIF/deletion → Tasks 5 + 8; §3.3 AI labeling → Task 11 (badge; SynthID survives the JPEG re-encode in Task 5's `SaveResultAsync` — do not add further transformations there). Not in scope by spec: §10.2–10.3 (DPA/transfer checks) and §13 (comparison protocol) are process steps for the owner, not code.
- **Known integration points**: Task 11 Step 2's five page-state bindings are the only intentionally open references — they resolve against the Option 2 page at execution time (Prerequisite Contract #4).
- **Type consistency check**: `GenerateVisualizationResponse` is a positional record in the API (Task 6) and a mutable class in the client (Task 10) — intentional (client rewrites `ImageUrl`); JSON shapes match (`photoToken`, `productId`, `imageUrl`, `cached`).
