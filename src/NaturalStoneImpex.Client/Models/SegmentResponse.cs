namespace NaturalStoneImpex.Client.Models;

public record SegmentResponse
{
    public string SessionToken { get; init; } = string.Empty;
    public string MaskPng { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
}
