using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public class VisualizerService : IVisualizerService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public VisualizerService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<VisualizerProductDto>> GetProductsAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<VisualizerProductDto>>("api/visualizer/products");
        return result ?? new List<VisualizerProductDto>();
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
