using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace NaturalStoneImpex.Client.Auth;

/// <summary>
/// Asks the server who is signed in (GET api/auth/me, authenticated by the HttpOnly admin cookie).
/// The answer is cached for the app's lifetime; call <see cref="NotifyChanged"/> to ask again.
/// </summary>
public class CookieAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly HttpClient _httpClient;
    private Task<AuthenticationState>? _cached;

    public CookieAuthStateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => _cached ??= FetchAsync();

    public void NotifyChanged()
    {
        _cached = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task<AuthenticationState> FetchAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/auth/me");
            if (response.StatusCode != HttpStatusCode.OK)
                return Anonymous;

            var me = await response.Content.ReadFromJsonAsync<MeResponse>();
            if (string.IsNullOrWhiteSpace(me?.Username))
                return Anonymous;

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, me.Username) }, "cookie");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (HttpRequestException)
        {
            return Anonymous;
        }
    }

    private sealed record MeResponse(string? Username);
}
