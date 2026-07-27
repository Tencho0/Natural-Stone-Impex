### Task 6: Segment endpoints + configuration + DI wiring

**Files:**
- Create: `src/NaturalStoneImpex.Api/Models/DTOs/SegmentPointDto.cs`
- Create: `src/NaturalStoneImpex.Api/Models/DTOs/SegmentResponse.cs`
- Modify: `src/NaturalStoneImpex.Api/Controllers/VisualizerController.cs`
- Modify: `src/NaturalStoneImpex.Api/Program.cs`
- Modify: `src/NaturalStoneImpex.Api/appsettings.json`

**Interfaces:**
- Consumes: `ISegmentationService`, `SamPoint`, `VisualizerOptions`, `EncodeGate`, `ISamModel`/`SamOnnxModel` (Tasks 3, 5).
- Produces (client Task 9 depends on these):
  - `POST /api/visualizer/segment` — multipart form: `photo` (file) + `points` (JSON string `[{"x":123.4,"y":56.7,"label":1}]`) → `200 { "sessionToken", "maskPng", "width", "height" }` (camelCase), or 400/429/503 `{ "error": "..." }`.
  - `POST /api/visualizer/segment/{sessionToken}` — JSON body `[{"x":..,"y":..,"label":..}]` → same 200 shape, or 404/503.

- [ ] **Step 1: Create the DTOs**

Create `src/NaturalStoneImpex.Api/Models/DTOs/SegmentPointDto.cs`:

```csharp
namespace NaturalStoneImpex.Api.Models.DTOs;

/// <summary>A tap point in original-photo pixel coordinates. Label: 1 = add, 0 = remove.</summary>
public record SegmentPointDto(double X, double Y, int Label);
```

Create `src/NaturalStoneImpex.Api/Models/DTOs/SegmentResponse.cs`:

```csharp
namespace NaturalStoneImpex.Api.Models.DTOs;

public record SegmentResponse
{
    public string SessionToken { get; init; } = string.Empty;
    public string MaskPng { get; init; } = string.Empty; // base64 grayscale PNG, white = selected
    public int Width { get; init; }
    public int Height { get; init; }
}
```

- [ ] **Step 2: Add the endpoints to VisualizerController**

Replace the content of `src/NaturalStoneImpex.Api/Controllers/VisualizerController.cs` with:

```csharp
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
```

- [ ] **Step 3: Wire DI and configuration**

In `src/NaturalStoneImpex.Api/Program.cs`, after the existing service registrations (`builder.Services.AddScoped<IInvoiceService, InvoiceService>();`) add:

```csharp
// Visualizer (see docs/visualizer-specification.md)
builder.Services.Configure<VisualizerOptions>(builder.Configuration.GetSection("Visualizer"));
builder.Services.AddMemoryCache(options => options.SizeLimit = 16); // embeddings are ~4 MB each
builder.Services.AddSingleton<EncodeGate>();
builder.Services.AddSingleton<ISamModel>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<VisualizerOptions>>().Value;
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    return new SamOnnxModel(
        Path.Combine(env.ContentRootPath, options.EncoderPath),
        Path.Combine(env.ContentRootPath, options.DecoderPath));
});
builder.Services.AddScoped<ISegmentationService, SegmentationService>();
```

and add `using NaturalStoneImpex.Api.Services.Segmentation;` to the usings.

In `src/NaturalStoneImpex.Api/appsettings.json` add a top-level section:

```json
  "Visualizer": {
    "Enabled": true,
    "EncoderPath": "MLModels/mobilesam-encoder.onnx",
    "DecoderPath": "MLModels/mobilesam-decoder.onnx",
    "MaxUploadBytes": 10485760,
    "MaxImageDimension": 2048,
    "MaxConcurrentEncodes": 2,
    "EmbeddingCacheMinutes": 15,
    "PerIpDailyLimit": 20,
    "GlobalDailyLimit": 500
  }
```

- [ ] **Step 4: Build and verify end-to-end with curl**

```powershell
dotnet build
dotnet run --project src/NaturalStoneImpex.Api
```

In a second terminal (any small JPG works as `photo.jpg`):

```powershell
curl.exe -k -X POST https://localhost:5001/api/visualizer/segment -F "photo=@photo.jpg" -F "points=[{\"x\":100,\"y\":100,\"label\":1}]"
```
Expected: `200` JSON with `sessionToken`, non-empty `maskPng`, `width`, `height` (needs models from Task 3 downloaded; otherwise expect `503 {"error":"Визуализаторът е временно недостъпен."}` — also a valid check).

```powershell
curl.exe -k -X POST https://localhost:5001/api/visualizer/segment/deadbeef -H "Content-Type: application/json" -d "[{\"x\":1,\"y\":1,\"label\":1}]"
```
Expected: `404 {"error":"Сесията е изтекла. Моля, качете снимката отново."}`.

- [ ] **Step 5: Run all tests**

Run: `dotnet test`
Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): segment endpoints with config and DI"
```

---

