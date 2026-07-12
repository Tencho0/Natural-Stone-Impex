using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NaturalStoneImpex.Api.Controllers;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Services;
using NaturalStoneImpex.Api.Services.Segmentation;

namespace NaturalStoneImpex.Api.Tests;

public class FakeProductServiceForController : IProductService
{
    public Task<PaginatedResponse<ProductListDto>> GetAllAsync(int? categoryId, string? search, int page, int pageSize, bool includeInactive)
        => throw new NotImplementedException();

    public Task<ProductDto?> GetByIdAsync(int id) => throw new NotImplementedException();

    public Task<ProductDto> CreateAsync(CreateProductRequest request) => throw new NotImplementedException();

    public Task<ProductDto?> UpdateAsync(int id, UpdateProductRequest request) => throw new NotImplementedException();

    public Task<(bool Success, string? Error)> DeleteAsync(int id) => throw new NotImplementedException();

    public Task<(string? ImagePath, string? Error)> UploadImageAsync(int id, Microsoft.AspNetCore.Http.IFormFile file)
        => throw new NotImplementedException();

    public Task<List<ProductListDto>> GetLowStockAsync(decimal threshold) => throw new NotImplementedException();

    public Task<List<VisualizerProductDto>> GetVisualizerProductsAsync() => throw new NotImplementedException();

    public Task<(string? TexturePath, string? Error)> UploadTextureAsync(int id, Microsoft.AspNetCore.Http.IFormFile file)
        => throw new NotImplementedException();
}

public class FakeSegmentationService : ISegmentationService
{
    public Task<SegmentOutcome> SegmentNewAsync(Stream photo, IReadOnlyList<SamPoint> points, string clientIp)
        => Task.FromResult(SegmentOutcome.Ok(new SegmentResult("token", "bWFzaw==", 10, 10)));

    public Task<SegmentOutcome> RefineAsync(string sessionToken, IReadOnlyList<SamPoint> points)
        => Task.FromResult(SegmentOutcome.Fail(404, "Сесията е изтекла. Моля, качете снимката отново."));
}

public class VisualizerControllerTests
{
    // Default JsonSerializerOptions escape non-ASCII characters (Cyrillic -> \uXXXX),
    // so Bulgarian text does not appear literally in the serialized string unless we
    // opt into a relaxed encoder here (test-only; does not affect the API's real
    // serialization behavior).
    private static readonly JsonSerializerOptions Utf8Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static VisualizerController CreateController() =>
        new(new FakeProductServiceForController(), new FakeSegmentationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };

    [Fact]
    public async Task Segment_without_photo_returns_bulgarian_error_shape()
    {
        var controller = CreateController();
        var result = await controller.Segment(null, "[{\"x\":1,\"y\":1,\"label\":1}]");
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Снимката е задължителна.", JsonSerializer.Serialize(bad.Value, Utf8Json));
    }

    [Fact]
    public async Task Segment_without_points_returns_bulgarian_error_shape()
    {
        var controller = CreateController();
        var photo = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "photo", "p.jpg");
        var result = await controller.Segment(photo, null);
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Моля, докоснете областта", JsonSerializer.Serialize(bad.Value, Utf8Json));
    }

    [Fact]
    public async Task Segment_with_malformed_points_returns_400_not_500()
    {
        var controller = CreateController();
        var photo = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "photo", "p.jpg");
        var result = await controller.Segment(photo, "{not json");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Refine_without_body_returns_bulgarian_error_shape()
    {
        var controller = CreateController();
        var result = await controller.Refine("sometoken", null);
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Моля, докоснете областта", JsonSerializer.Serialize(bad.Value, Utf8Json));
    }
}
