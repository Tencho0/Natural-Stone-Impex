using System.Net.Http.Json;
using System.Text.Json;
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public class InvoiceService : IInvoiceService
{
    private readonly HttpClient _httpClient;

    public InvoiceService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedResponse<InvoiceListDto>> GetAllAsync(int page, int pageSize)
    {
        var result = await _httpClient.GetFromJsonAsync<PaginatedResponse<InvoiceListDto>>(
            $"api/invoices?page={page}&pageSize={pageSize}");
        return result ?? new PaginatedResponse<InvoiceListDto>();
    }

    public async Task<InvoiceDetailDto> GetByIdAsync(int id)
    {
        var result = await _httpClient.GetFromJsonAsync<InvoiceDetailDto>($"api/invoices/{id}");
        return result!;
    }

    public async Task<(CreateInvoiceResponse? Response, string? Error)> CreateAsync(CreateInvoiceRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/invoices", request);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            try
            {
                var errorDoc = JsonDocument.Parse(content);
                if (errorDoc.RootElement.TryGetProperty("error", out var errorMessage))
                {
                    return (null, errorMessage.GetString() ?? "Възникна неочаквана грешка.");
                }
            }
            catch (JsonException)
            {
            }

            return (null, "Възникна неочаквана грешка.");
        }

        var result = await response.Content.ReadFromJsonAsync<CreateInvoiceResponse>();
        return (result, null);
    }
}
