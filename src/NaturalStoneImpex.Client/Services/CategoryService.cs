using System.Net.Http.Json;
using System.Text.Json;
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public class CategoryService : ICategoryService
{
    private readonly HttpClient _httpClient;

    public CategoryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<CategoryDto>> GetAllAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<CategoryDto>>("api/categories");
        return result ?? new List<CategoryDto>();
    }

    public async Task<string?> CreateAsync(string name)
    {
        var response = await _httpClient.PostAsJsonAsync("api/categories", new { name });

        if (!response.IsSuccessStatusCode)
        {
            return await ExtractErrorAsync(response);
        }

        return null;
    }

    public async Task<string?> UpdateAsync(int id, string name)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/categories/{id}", new { name });

        if (!response.IsSuccessStatusCode)
        {
            return await ExtractErrorAsync(response);
        }

        return null;
    }

    public async Task<string?> DeleteAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/categories/{id}");

        if (!response.IsSuccessStatusCode)
        {
            return await ExtractErrorAsync(response);
        }

        return null;
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
