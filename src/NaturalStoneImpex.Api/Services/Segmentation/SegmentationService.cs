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
