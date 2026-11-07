using System.Runtime.CompilerServices;
using NaturalStoneImpex.Api.Services.Segmentation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NaturalStoneImpex.Api.Tests;

public class SamOnnxModelTests
{
    private const string EncoderFile = "mobilesam-encoder.onnx";
    private const string DecoderFile = "mobilesam-decoder.onnx";

    /// <summary>Locates the ONNX models: copied next to the test assembly (the API project
    /// marks them as Content), else the source tree relative to the default bin folder,
    /// else the source tree relative to this file (works with --artifacts-path builds).
    /// Returns null when the models were never downloaded.</summary>
    private static string? FindModelsDir([CallerFilePath] string thisFile = "")
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "MLModels"),
            Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "src", "NaturalStoneImpex.Api", "MLModels"),
            Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "src", "NaturalStoneImpex.Api", "MLModels")
        };
        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(dir => File.Exists(Path.Combine(dir, EncoderFile))
                                   && File.Exists(Path.Combine(dir, DecoderFile)));
    }

    private static readonly Lazy<SamOnnxModel?> SharedModel = new(() =>
    {
        var dir = FindModelsDir();
        return dir is null
            ? null
            : new SamOnnxModel(Path.Combine(dir, EncoderFile), Path.Combine(dir, DecoderFile));
    });

    [Fact]
    public void Encode_and_decode_segments_the_tapped_region()
    {
        var model = SharedModel.Value;
        if (model is null)
            return; // models not downloaded — skip (see scripts/download-visualizer-models.ps1)

        // NOTE: the brief specified SixLabors.ImageSharp.Drawing for RectangularPolygon/Fill,
        // but ImageSharp.Drawing 3.x requires a paid Six Labors commercial license key at build
        // time (see SixLabors.Licensing.ValidateLicenseTask in its .targets file) — a hard
        // external blocker unrelated to the tensor contract. The core SixLabors.ImageSharp
        // package has no such requirement, so the gray "driveway" rectangle is filled here via
        // plain pixel access instead, producing an identical synthetic photo without the
        // licensed dependency.
        using var image = new Image<Rgb24>(1024, 768, new Rgb24(60, 140, 60));
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 400; y < 700; y++) // height 300, starting at y=400
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 300; x < 900; x++) // width 600, starting at x=300
                    row[x] = new Rgb24(128, 126, 124);
            }
        }); // gray rectangle = "driveway"

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

    /// <summary>Regression: the decoder's full-resolution "masks" output of this export is
    /// geometrically misregistered (Y stretched for landscape, X and Y for portrait/square).
    /// The returned mask must line up with the tapped object for every aspect ratio.</summary>
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1080, 1920)]
    [InlineData(1024, 1024)]
    public void Decode_mask_is_registered_with_the_photo(int width, int height)
    {
        var model = SharedModel.Value;
        if (model is null)
            return; // models not downloaded — skip (see scripts/download-visualizer-models.ps1)

        // Red rectangle at x∈[0.30,0.50), y∈[0.60,0.80) on a neutral gray background.
        int rx0 = (int)(0.30 * width), rx1 = (int)(0.50 * width);
        int ry0 = (int)(0.60 * height), ry1 = (int)(0.80 * height);
        using var image = new Image<Rgb24>(width, height, new Rgb24(200, 200, 200));
        image.ProcessPixelRows(accessor =>
        {
            for (var y = ry0; y < ry1; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = rx0; x < rx1; x++)
                    row[x] = new Rgb24(200, 30, 30);
            }
        });

        var embedding = model.Encode(image);
        var tap = new SamPoint((rx0 + rx1) / 2f, (ry0 + ry1) / 2f, 1);
        var logits = model.Decode(embedding, new[] { tap });

        Assert.Equal(height, logits.GetLength(0));
        Assert.Equal(width, logits.GetLength(1));

        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (logits[y, x] <= 0) continue;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }

        Assert.True(maxX >= 0, "mask must not be empty");
        var tolX = 0.015 * width;
        var tolY = 0.015 * height;
        var actual = $"mask bbox x∈[{minX},{maxX}] y∈[{minY},{maxY}], " +
                     $"expected x∈[{rx0},{rx1 - 1}] y∈[{ry0},{ry1 - 1}] ({width}x{height})";
        Assert.True(Math.Abs(minX - rx0) <= tolX, actual);
        Assert.True(Math.Abs(maxX - (rx1 - 1)) <= tolX, actual);
        Assert.True(Math.Abs(minY - ry0) <= tolY, actual);
        Assert.True(Math.Abs(maxY - (ry1 - 1)) <= tolY, actual);
    }

    /// <summary>Builds a 256x256 low-res logit grid (padded 1024x1024 frame) that is +10 inside
    /// the half-open low-res block [x0,x1) x [y0,y1) and -10 elsewhere.</summary>
    private static float[] LowResWithBlock(int x0, int x1, int y0, int y1)
    {
        var low = new float[256 * 256];
        for (var y = 0; y < 256; y++)
            for (var x = 0; x < 256; x++)
                low[y * 256 + x] = x >= x0 && x < x1 && y >= y0 && y < y1 ? 10f : -10f;
        return low;
    }

    private static (int MinX, int MaxX, int MinY, int MaxY) PositiveBbox(float[,] mask)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = 0; y < mask.GetLength(0); y++)
            for (var x = 0; x < mask.GetLength(1); x++)
            {
                if (mask[y, x] <= 0) continue;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
        return (minX, maxX, minY, maxY);
    }

    // Low-res block [64,128) x [72,108). Original px per low-res px = 4 / scale, where
    // scale = 1024 / max(W, H); expected bbox = block edges * (4 / scale), inclusive max.
    [Theory]
    [InlineData(1920, 1080, 64, 128, 72, 108, 480, 959, 540, 809)]   // landscape: 7.5 px / cell
    [InlineData(1080, 1920, 72, 108, 64, 128, 540, 809, 480, 959)]   // portrait: 7.5 px / cell
    [InlineData(1024, 1024, 64, 128, 72, 108, 256, 511, 288, 431)]   // square: 4 px / cell
    [InlineData(512, 384, 64, 128, 72, 108, 128, 255, 144, 215)]     // upscaled small photo: 2 px / cell
    public void UpsampleLowResLogits_maps_low_res_block_to_original_pixels(
        int width, int height, int bx0, int bx1, int by0, int by1,
        int expMinX, int expMaxX, int expMinY, int expMaxY)
    {
        var scale = 1024f / Math.Max(width, height);
        var low = LowResWithBlock(bx0, bx1, by0, by1);

        var mask = SamOnnxModel.UpsampleLowResLogits(low, 256, width, height, scale);

        Assert.Equal(height, mask.GetLength(0));
        Assert.Equal(width, mask.GetLength(1));
        var (minX, maxX, minY, maxY) = PositiveBbox(mask);
        Assert.InRange(minX, expMinX - 1, expMinX + 1);
        Assert.InRange(maxX, expMaxX - 1, expMaxX + 1);
        Assert.InRange(minY, expMinY - 1, expMinY + 1);
        Assert.InRange(maxY, expMaxY - 1, expMaxY + 1);
    }

    [Fact]
    public void UpsampleLowResLogits_interpolates_between_cell_centres()
    {
        // Square 1024 photo: 4 original px per low-res cell. Cell (10,0) = 0, cell (11,0) = 4.
        var low = new float[256 * 256];
        low[11] = 4f;
        low[256 + 11] = 4f; // keep row 1 identical so the vertical blend does not dilute it

        var mask = SamOnnxModel.UpsampleLowResLogits(low, 256, 1024, 1024, 1f);

        // Cell 10 centre = original x 41.5 → pixel 41/42 straddle it; cell 11 centre = x 45.5.
        Assert.Equal(0.5f, mask[0, 42], 3); // x=42 → low-res 10.125
        Assert.Equal(3.5f, mask[0, 45], 3); // x=45 → low-res 10.875
    }
}
