namespace NaturalStoneImpex.Client.Models;

/// <summary>Tap point in photo pixel coordinates. Label: 1 = add area, 0 = remove area.</summary>
public record SegmentPoint(double X, double Y, int Label);
