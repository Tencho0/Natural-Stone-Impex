### Task 9: Client models + VisualizerService

**Files:**
- Create: `src/NaturalStoneImpex.Client/Models/VisualizerProductDto.cs`
- Create: `src/NaturalStoneImpex.Client/Models/SegmentPoint.cs`
- Create: `src/NaturalStoneImpex.Client/Models/SegmentResponse.cs`
- Modify: `src/NaturalStoneImpex.Client/Models/ProductDto.cs`
- Modify: `src/NaturalStoneImpex.Client/Models/CreateProductRequest.cs`
- Modify: `src/NaturalStoneImpex.Client/Models/UpdateProductRequest.cs`
- Create: `src/NaturalStoneImpex.Client/Services/IVisualizerService.cs`
- Create: `src/NaturalStoneImpex.Client/Services/VisualizerService.cs`
- Modify: `src/NaturalStoneImpex.Client/Program.cs`

**Interfaces:**
- Consumes: HTTP contract from Tasks 2 and 6.
- Produces (Tasks 10–13 depend on these):

```csharp
public interface IVisualizerService
{
    Task<List<VisualizerProductDto>> GetProductsAsync();                       // paths resolved to absolute URLs
    Task<(SegmentResponse? Result, string? Error)> SegmentAsync(byte[] photoBytes, List<SegmentPoint> points);
    Task<(SegmentResponse? Result, string? Error, bool SessionExpired)> RefineAsync(string sessionToken, List<SegmentPoint> points);
}
```

- [ ] **Step 1: Create the models**

Create `src/NaturalStoneImpex.Client/Models/VisualizerProductDto.cs`:

```csharp
namespace NaturalStoneImpex.Client.Models;

public record VisualizerProductDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ImagePath { get; set; }          // set: resolved to absolute URL client-side
    public string TexturePath { get; set; } = string.Empty;
    public decimal TextureWidthMeters { get; init; }
    public decimal PriceWithoutVat { get; init; }
    public decimal VatAmount { get; init; }
    public decimal PriceWithVat { get; init; }
    public int Unit { get; init; }
    public string UnitDisplay { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
}
```

Create `src/NaturalStoneImpex.Client/Models/SegmentPoint.cs`:

```csharp
namespace NaturalStoneImpex.Client.Models;

/// <summary>Tap point in photo pixel coordinates. Label: 1 = add area, 0 = remove area.</summary>
public record SegmentPoint(double X, double Y, int Label);
```

Create `src/NaturalStoneImpex.Client/Models/SegmentResponse.cs`:

```csharp
namespace NaturalStoneImpex.Client.Models;

public record SegmentResponse
{
    public string SessionToken { get; init; } = string.Empty;
    public string MaskPng { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
}
```

In `src/NaturalStoneImpex.Client/Models/ProductDto.cs` add after `IsActive` (match the API ProductDto from Task 1):

```csharp
    public bool IsVisualizerEnabled { get; init; }
    public string? TextureImagePath { get; set; }
    public decimal TextureWidthMeters { get; init; }
```

In **both** client `CreateProductRequest.cs` and `UpdateProductRequest.cs` add after `StockQuantity` (mirror the API requests from Task 1 — check the client files' property style and keep it):

```csharp
    public bool IsVisualizerEnabled { get; set; }
    public decimal TextureWidthMeters { get; set; } = 1.00m;
```

- [ ] **Step 2: Create the service**

Create `src/NaturalStoneImpex.Client/Services/IVisualizerService.cs`:

```csharp
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public interface IVisualizerService
{
    Task<List<VisualizerProductDto>> GetProductsAsync();
    Task<(SegmentResponse? Result, string? Error)> SegmentAsync(byte[] photoBytes, List<SegmentPoint> points);
    Task<(SegmentResponse? Result, string? Error, bool SessionExpired)> RefineAsync(string sessionToken, List<SegmentPoint> points);
}
```

Create `src/NaturalStoneImpex.Client/Services/VisualizerService.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public class VisualizerService : IVisualizerService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public VisualizerService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiBaseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "";
    }

    public async Task<List<VisualizerProductDto>> GetProductsAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<VisualizerProductDto>>("api/visualizer/products");
        if (result is null) return new List<VisualizerProductDto>();
        foreach (var product in result)
        {
            product.ImagePath = Resolve(product.ImagePath);
            product.TexturePath = Resolve(product.TexturePath)!;
        }
        return result;
    }

    public async Task<(SegmentResponse? Result, string? Error)> SegmentAsync(byte[] photoBytes, List<SegmentPoint> points)
    {
        using var content = new MultipartFormDataContent();
        var photoContent = new ByteArrayContent(photoBytes);
        photoContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(photoContent, "photo", "photo.jpg");
        content.Add(new StringContent(JsonSerializer.Serialize(points, Json)), "points");

        try
        {
            var response = await _httpClient.PostAsync("api/visualizer/segment", content);
            if (!response.IsSuccessStatusCode)
                return (null, await ExtractErrorAsync(response));
            return (await response.Content.ReadFromJsonAsync<SegmentResponse>(), null);
        }
        catch (HttpRequestException)
        {
            return (null, "Възникна грешка при връзката със сървъра. Моля, опитайте отново.");
        }
    }

    public async Task<(SegmentResponse? Result, string? Error, bool SessionExpired)> RefineAsync(
        string sessionToken, List<SegmentPoint> points)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/visualizer/segment/{sessionToken}", points, Json);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return (null, null, true);
            if (!response.IsSuccessStatusCode)
                return (null, await ExtractErrorAsync(response), false);
            return (await response.Content.ReadFromJsonAsync<SegmentResponse>(), null, false);
        }
        catch (HttpRequestException)
        {
            return (null, "Възникна грешка при връзката със сървъра. Моля, опитайте отново.", false);
        }
    }

    private string? Resolve(string? path) =>
        string.IsNullOrEmpty(path) ? path : $"{_apiBaseUrl}{path}";

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        try
        {
            var errorDoc = JsonDocument.Parse(content);
            if (errorDoc.RootElement.TryGetProperty("error", out var errorMessage))
                return errorMessage.GetString() ?? "Възникна неочаквана грешка.";
        }
        catch (JsonException)
        {
        }
        return "Възникна неочаквана грешка.";
    }
}
```

- [ ] **Step 3: Register and build**

In `src/NaturalStoneImpex.Client/Program.cs`, after `builder.Services.AddScoped<IInvoiceService, InvoiceService>();` add:

```csharp
builder.Services.AddScoped<IVisualizerService, VisualizerService>();
```

Run: `dotnet build`
Expected: success.

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): client models and API service"
```

---

