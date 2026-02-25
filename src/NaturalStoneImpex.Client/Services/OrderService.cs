using System.Net.Http.Json;
using System.Text.Json;
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public class OrderService : IOrderService
{
    private readonly HttpClient _httpClient;

    public OrderService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(CreateOrderResponse? Response, string? Error)> PlaceOrderAsync(CreateOrderRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/orders", request);

        if (!response.IsSuccessStatusCode)
        {
            return (null, await ExtractErrorAsync(response));
        }

        var result = await response.Content.ReadFromJsonAsync<CreateOrderResponse>();
        return (result, null);
    }

    public async Task<PaginatedResponse<OrderListDto>> GetAllAsync(int? status, int page, int pageSize)
    {
        var url = $"api/orders?page={page}&pageSize={pageSize}";
        if (status.HasValue)
            url += $"&status={status.Value}";

        var result = await _httpClient.GetFromJsonAsync<PaginatedResponse<OrderListDto>>(url);
        return result ?? new PaginatedResponse<OrderListDto>();
    }

    public async Task<OrderDetailDto> GetByIdAsync(int id)
    {
        var result = await _httpClient.GetFromJsonAsync<OrderDetailDto>($"api/orders/{id}");
        return result!;
    }

    public async Task<(string? Error, List<StockShortageDetail>? Details)> ConfirmAsync(int id)
    {
        var response = await _httpClient.PutAsync($"api/orders/{id}/confirm", null);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            try
            {
                var errorDoc = JsonDocument.Parse(content);
                var error = "Възникна неочаквана грешка.";
                List<StockShortageDetail>? details = null;

                if (errorDoc.RootElement.TryGetProperty("error", out var errorProp))
                    error = errorProp.GetString() ?? error;

                if (errorDoc.RootElement.TryGetProperty("details", out var detailsProp))
                {
                    details = JsonSerializer.Deserialize<List<StockShortageDetail>>(
                        detailsProp.GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }

                return (error, details);
            }
            catch (JsonException)
            {
                return ("Възникна неочаквана грешка.", null);
            }
        }
        return (null, null);
    }

    public async Task<string?> CompleteAsync(int id)
    {
        var response = await _httpClient.PutAsync($"api/orders/{id}/complete", null);
        if (!response.IsSuccessStatusCode)
            return await ExtractErrorAsync(response);
        return null;
    }

    public async Task<string?> CancelAsync(int id)
    {
        var response = await _httpClient.PutAsync($"api/orders/{id}/cancel", null);
        if (!response.IsSuccessStatusCode)
            return await ExtractErrorAsync(response);
        return null;
    }

    public async Task<string?> SetDeliveryFeeAsync(int id, decimal deliveryFee)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/orders/{id}/delivery-fee", new { deliveryFee });
        if (!response.IsSuccessStatusCode)
            return await ExtractErrorAsync(response);
        return null;
    }

    public async Task<OrderStatsDto> GetStatsAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<OrderStatsDto>("api/orders/stats");
        return result ?? new OrderStatsDto();
    }

    public async Task<List<OrderListDto>> GetRecentAsync(int count)
    {
        var result = await _httpClient.GetFromJsonAsync<List<OrderListDto>>($"api/orders/recent?count={count}");
        return result ?? new List<OrderListDto>();
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
