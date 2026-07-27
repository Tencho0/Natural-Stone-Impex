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

