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
