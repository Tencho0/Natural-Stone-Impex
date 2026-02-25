using System.Net.Http.Json;
using System.Text.Json;
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public class ProductService : IProductService
{
    private readonly HttpClient _httpClient;

    public ProductService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedResponse<ProductListDto>> GetAllAsync(int? categoryId = null, string? search = null, int page = 1, int pageSize = 20)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (categoryId.HasValue)
            queryParams.Add($"categoryId={categoryId.Value}");

        if (!string.IsNullOrWhiteSpace(search))
            queryParams.Add($"search={Uri.EscapeDataString(search)}");

        var url = $"api/products?{string.Join("&", queryParams)}";
        var result = await _httpClient.GetFromJsonAsync<PaginatedResponse<ProductListDto>>(url);
        return result ?? new PaginatedResponse<ProductListDto>();
    }

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ProductDto>($"api/products/{id}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<(int? ProductId, string? Error)> CreateAsync(CreateProductRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/products", request);

        if (!response.IsSuccessStatusCode)
        {
            return (null, await ExtractErrorAsync(response));
        }

        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        return (product?.Id, null);
    }

    public async Task<string?> UpdateAsync(int id, UpdateProductRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/products/{id}", request);

        if (!response.IsSuccessStatusCode)
        {
            return await ExtractErrorAsync(response);
        }

        return null;
    }

    public async Task<string?> DeleteAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/products/{id}");

        if (!response.IsSuccessStatusCode)
        {
            return await ExtractErrorAsync(response);
        }

        return null;
    }

    public async Task<string?> UploadImageAsync(int id, Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        content.Add(streamContent, "image", fileName);

        var response = await _httpClient.PostAsync($"api/products/{id}/image", content);

        if (!response.IsSuccessStatusCode)
        {
            return await ExtractErrorAsync(response);
        }

        return null;
    }

    public async Task<List<ProductListDto>> GetLowStockAsync(decimal threshold = 10)
    {
        var result = await _httpClient.GetFromJsonAsync<List<ProductListDto>>($"api/products/low-stock?threshold={threshold}");
        return result ?? new List<ProductListDto>();
    }

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        try
        {
            var errorDoc = JsonDocument.Parse(content);
            if (errorDoc.RootElement.TryGetProperty("error", out var errorMessage))
            {
                return errorMessage.GetString() ?? "Възникна неочаквана грешка.";
            }
        }
        catch (JsonException)
        {
        }

        return "Възникна неочаквана грешка.";
    }
}
