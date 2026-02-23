using System.Net.Http.Json;
using System.Text.Json;
using NaturalStoneImpex.Client.Auth;
using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly CustomAuthStateProvider _authStateProvider;

    public AuthService(HttpClient httpClient, CustomAuthStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _authStateProvider = authStateProvider;
    }

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(_authStateProvider.Token);

    public async Task<string?> LoginAsync(string username, string password)
    {
        var request = new LoginRequest
        {
            Username = username,
            Password = password
        };

        var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();

            try
            {
                var errorDoc = JsonDocument.Parse(errorContent);
                if (errorDoc.RootElement.TryGetProperty("error", out var errorMessage))
                {
                    return errorMessage.GetString();
                }
            }
            catch (JsonException)
            {
                // If response is not valid JSON, return a generic message
            }

            return "Невалидно потребителско име или парола.";
        }

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();

        if (loginResponse is null || string.IsNullOrWhiteSpace(loginResponse.Token))
        {
            return "Възникна грешка при влизане.";
        }

        _authStateProvider.SetToken(loginResponse.Token);
        return null;
    }

    public void Logout()
    {
        _authStateProvider.SetToken(null);
    }
}
