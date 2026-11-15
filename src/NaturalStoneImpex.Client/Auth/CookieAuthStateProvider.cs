using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace NaturalStoneImpex.Client.Auth;

/// <summary>
/// Asks the server who is signed in (GET api/auth/me, authenticated by the HttpOnly admin cookie).
/// A definite answer (signed in, or 401/403) is cached for the app's lifetime; call
/// <see cref="NotifyChanged"/> to ask again. A failed lookup (exception or other status) counts
/// as anonymous but is not cached, so the next check retries.
/// </summary>
public class CookieAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly HttpClient _httpClient;
    private Task<(AuthenticationState State, bool Cacheable)>? _lookup;

    public CookieAuthStateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var lookup = _lookup ??= LookupAsync();
        var (state, cacheable) = await lookup;

        if (!cacheable && ReferenceEquals(_lookup, lookup))
            _lookup = null;

        return state;
    }

    public void NotifyChanged()
    {
        _lookup = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task<(AuthenticationState, bool)> LookupAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/auth/me");

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return (Anonymous, true);

            if (response.StatusCode != HttpStatusCode.OK)
                return (Anonymous, false);

            var me = await response.Content.ReadFromJsonAsync<MeResponse>();
            if (string.IsNullOrWhiteSpace(me?.Username))
                return (Anonymous, false);

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, me.Username) }, "cookie");
            return (new AuthenticationState(new ClaimsPrincipal(identity)), true);
        }
        catch (Exception)
        {
            return (Anonymous, false);
        }
    }

    private sealed record MeResponse(string? Username);
}
