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
