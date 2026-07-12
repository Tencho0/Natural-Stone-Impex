namespace NaturalStoneImpex.Api.Services.Segmentation;

public record SegmentResult(string SessionToken, string MaskPng, int Width, int Height);

public record SegmentOutcome(int StatusCode, string? Error, SegmentResult? Result)
{
    public static SegmentOutcome Ok(SegmentResult result) => new(200, null, result);
    public static SegmentOutcome Fail(int statusCode, string error) => new(statusCode, error, null);
}

public interface ISegmentationService
{
    Task<SegmentOutcome> SegmentNewAsync(Stream photo, IReadOnlyList<SamPoint> points, string clientIp);
    Task<SegmentOutcome> RefineAsync(string sessionToken, IReadOnlyList<SamPoint> points);
}
