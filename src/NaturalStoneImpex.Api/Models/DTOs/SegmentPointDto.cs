namespace NaturalStoneImpex.Api.Models.DTOs;

/// <summary>A tap point in original-photo pixel coordinates. Label: 1 = add, 0 = remove.</summary>
public record SegmentPointDto(double X, double Y, int Label);
