using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Models.Entities;
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
    private static byte[] TestPhotoBytes(int width = 400, int height = 300)
    {
        using var image = new Image<Rgb24>(width, height, new Rgb24(100, 100, 100));
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

    [Fact]
    public async Task Oversized_declared_dimensions_returns_400_and_persists_failed_row()
    {
        // A real 100x100 JPEG, but MaxImageDimension is set so low that 100 > 2 * 20 —
        // this must be rejected by the cheap IdentifyAsync header check before any decode.
        var (service, _, db) = CreateService(new VisualizerOptions { MaxImageDimension = 20 });
        using var photo = new MemoryStream(TestPhotoBytes(100, 100));

        var outcome = await service.SegmentNewAsync(photo, Tap(), "9.9.9.9");

        Assert.Equal(400, outcome.StatusCode);
        Assert.Equal("Моля, качете снимка във формат JPG или PNG до 10 MB.", outcome.Error);
        var row = Assert.Single(db.VisualizationRequests.ToList());
        Assert.Equal(VisualizationStatus.Failed, row.Status);
    }

    [Fact]
    public async Task Invalid_image_persists_failed_row_and_exhausts_per_ip_quota()
    {
        var (service, _, db) = CreateService(new VisualizerOptions { PerIpDailyLimit = 2 });

        for (var i = 0; i < 2; i++)
        {
            using var junk = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
            var outcome = await service.SegmentNewAsync(junk, Tap(), "8.8.8.8");
            Assert.Equal(400, outcome.StatusCode);
        }

        // Failed attempts must count toward the quota — the table now has 2 Failed rows
        // for this IP, exhausting the PerIpDailyLimit of 2.
        Assert.Equal(2, db.VisualizationRequests.ToList().Count(r => r.Status == VisualizationStatus.Failed));

        using var third = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        var blocked = await service.SegmentNewAsync(third, Tap(), "8.8.8.8");

        Assert.Equal(429, blocked.StatusCode);
        Assert.Equal("Достигнахте дневния лимит за визуализации. Опитайте отново утре.", blocked.Error);
    }

    [Fact]
    public async Task Refine_beyond_ceiling_returns_429()
    {
        var (service, _, _) = CreateService();
        using var photo = new MemoryStream(TestPhotoBytes());
        var first = await service.SegmentNewAsync(photo, Tap(), "3.3.3.3");
        var token = first.Result!.SessionToken;

        for (var i = 0; i < 200; i++)
        {
            var outcome = await service.RefineAsync(token, Tap());
            Assert.Equal(200, outcome.StatusCode);
        }

        var blocked = await service.RefineAsync(token, Tap());

        Assert.Equal(429, blocked.StatusCode);
        Assert.Equal("Достигнахте дневния лимит за визуализации. Опитайте отново утре.", blocked.Error);
    }
}
