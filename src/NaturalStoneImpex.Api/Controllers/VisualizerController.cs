using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Services;
using NaturalStoneImpex.Api.Services.Segmentation;

namespace NaturalStoneImpex.Api.Controllers;

[ApiController]
[Route("api/visualizer")]
public class VisualizerController : ControllerBase
{
    private const string ErrorTooManyPoints = "Моля, докоснете областта, която искате да покриете.";
    private const int MaxPoints = 50;

    private static readonly JsonSerializerOptions PointsJson = new(JsonSerializerDefaults.Web);

    private readonly IProductService _productService;
    private readonly ISegmentationService _segmentationService;
    private readonly VisualizerOptions _options;

    public VisualizerController(IProductService productService, ISegmentationService segmentationService,
        IOptions<VisualizerOptions> options)
    {
        _productService = productService;
        _segmentationService = segmentationService;
        _options = options.Value;
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _productService.GetVisualizerProductsAsync();
        return Ok(products);
    }

    [HttpPost("segment")]
    [RequestSizeLimit(12_000_000)]
    public async Task<IActionResult> Segment(IFormFile? photo, [FromForm] string? points)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(new { error = "Снимката е задължителна." });

        if (photo.Length > _options.MaxUploadBytes)
            return BadRequest(new { error = "Моля, качете снимка във формат JPG или PNG до 10 MB." });

        var parsed = ParsePoints(points);
        if (parsed is null || parsed.Count == 0)
            return BadRequest(new { error = ErrorTooManyPoints });
        if (parsed.Count > MaxPoints)
            return BadRequest(new { error = ErrorTooManyPoints });

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await using var stream = photo.OpenReadStream();
        var outcome = await _segmentationService.SegmentNewAsync(stream, parsed, clientIp);
        return ToActionResult(outcome);
    }

    [HttpPost("segment/{sessionToken}")]
    public async Task<IActionResult> Refine(string sessionToken, [FromBody] List<SegmentPointDto>? points)
    {
        if (points is null || points.Count == 0)
            return BadRequest(new { error = ErrorTooManyPoints });
        if (points.Count > MaxPoints)
            return BadRequest(new { error = ErrorTooManyPoints });

        var outcome = await _segmentationService.RefineAsync(sessionToken,
            points.Select(p => new SamPoint((float)p.X, (float)p.Y, p.Label)).ToList());
        return ToActionResult(outcome);
    }

    private static List<SamPoint>? ParsePoints(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var dtos = JsonSerializer.Deserialize<List<SegmentPointDto>>(json, PointsJson);
            return dtos?.Select(p => new SamPoint((float)p.X, (float)p.Y, p.Label)).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private IActionResult ToActionResult(SegmentOutcome outcome)
    {
        if (outcome.StatusCode != 200 || outcome.Result is null)
            return StatusCode(outcome.StatusCode, new { error = outcome.Error });

        return Ok(new SegmentResponse
        {
            SessionToken = outcome.Result.SessionToken,
            MaskPng = outcome.Result.MaskPng,
            Width = outcome.Result.Width,
            Height = outcome.Result.Height
        });
    }
}
