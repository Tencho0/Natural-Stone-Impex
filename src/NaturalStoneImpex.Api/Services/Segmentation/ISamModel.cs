using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NaturalStoneImpex.Api.Services.Segmentation;

/// <summary>A point prompt in original-photo pixel coordinates. Label: 1 = include, 0 = exclude.</summary>
public record SamPoint(float X, float Y, int Label);

/// <summary>Encoder output plus the geometry needed to run the decoder. ~4 MB per photo.</summary>
public record SamEmbedding(float[] Data, float Scale, int OrigWidth, int OrigHeight);

public interface ISamModel
{
    bool IsAvailable { get; }
    SamEmbedding Encode(Image<Rgb24> image);
    /// <returns>Mask logits [OrigHeight, OrigWidth]; values &gt; 0 are selected.</returns>
    float[,] Decode(SamEmbedding embedding, IReadOnlyList<SamPoint> points);
}
