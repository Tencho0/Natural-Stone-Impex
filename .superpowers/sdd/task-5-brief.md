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

