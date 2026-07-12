using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public interface IVisualizerService
{
    Task<List<VisualizerProductDto>> GetProductsAsync();
    Task<(SegmentResponse? Result, string? Error)> SegmentAsync(byte[] photoBytes, List<SegmentPoint> points);
    Task<(SegmentResponse? Result, string? Error, bool SessionExpired)> RefineAsync(string sessionToken, List<SegmentPoint> points);
}
