namespace NaturalStoneImpex.Api.Models.DTOs;

public record SegmentResponse
{
    public string SessionToken { get; init; } = string.Empty;
    public string MaskPng { get; init; } = string.Empty; // base64 grayscale PNG, white = selected
    public int Width { get; init; }
    public int Height { get; init; }
}
