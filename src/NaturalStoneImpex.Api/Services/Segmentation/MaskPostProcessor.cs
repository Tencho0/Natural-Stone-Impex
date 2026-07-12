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
