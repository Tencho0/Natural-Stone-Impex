using NaturalStoneImpex.Api.Services.Segmentation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
