using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NaturalStoneImpex.Client.Services;

/// <summary>
/// Calls POST /api/cart/items on the same host. The HttpClient carries CsrfHeaderHandler, which adds the
/// X-NSI-Request header the endpoint requires. The server writes the nsi_cart cookie; prices never travel.
/// </summary>
public class CartApi : ICartApi
{
    public const string GenericError = "Неуспешно добавяне в количката. Опитайте отново.";

    private readonly HttpClient _httpClient;

    public CartApi(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> AddAsync(int productId, decimal quantity)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("api/cart/items", new { productId, quantity });

            if (response.StatusCode == HttpStatusCode.NoContent)
                return null;

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
                if (!string.IsNullOrWhiteSpace(body?.Error))
                    return body.Error;
            }

            return GenericError;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            // Network failure, timeout, or a 400 whose body is not the { "error": "..." } shape.
            return GenericError;
        }
    }

    private sealed record ErrorBody(string? Error);
}
