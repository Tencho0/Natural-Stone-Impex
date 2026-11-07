using Microsoft.AspNetCore.Components;

namespace NaturalStoneImpex.Client.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigation;

    public AuthService(HttpClient httpClient, NavigationManager navigation)
    {
        _httpClient = httpClient;
        _navigation = navigation;
    }

    public async Task LogoutAsync()
    {
        await _httpClient.PostAsync("api/auth/logout", null);
        _navigation.NavigateTo("/admin/login", forceLoad: true);
    }
}
