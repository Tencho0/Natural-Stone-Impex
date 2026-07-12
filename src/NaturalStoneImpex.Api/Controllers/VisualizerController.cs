using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Services;
using NaturalStoneImpex.Api.Services.Segmentation;

namespace NaturalStoneImpex.Api.Controllers;

[ApiController]
[Route("api/visualizer")]
public class VisualizerController : ControllerBase
{
    private static readonly JsonSerializerOptions PointsJson = new(JsonSerializerDefaults.Web);

    private readonly IProductService _productService;
    private readonly ISegmentationService _segmentationService;

    public VisualizerController(IProductService productService, ISegmentationService segmentationService)
    {
        _productService = productService;
        _segmentationService = segmentationService;
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _productService.GetVisualizerProductsAsync();
        return Ok(products);
    }

    [HttpPost("segment")]
    [RequestSizeLimit(12_000_000)]
    public async Task<IActionResult> Segment(IFormFile photo, [FromForm] string points)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(new { error = "Снимката е задължителна." });

        var parsed = ParsePoints(points);
        if (parsed is null || parsed.Count == 0)
            return BadRequest(new { error = "Моля, докоснете областта, която искате да покриете." });

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await using var stream = photo.OpenReadStream();
        var outcome = await _segmentationService.SegmentNewAsync(stream, parsed, clientIp);
        return ToActionResult(outcome);
    }

    [HttpPost("segment/{sessionToken}")]
    public async Task<IActionResult> Refine(string sessionToken, [FromBody] List<SegmentPointDto> points)
    {
        if (points is null || points.Count == 0)
            return BadRequest(new { error = "Моля, докоснете областта, която искате да покриете." });

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
