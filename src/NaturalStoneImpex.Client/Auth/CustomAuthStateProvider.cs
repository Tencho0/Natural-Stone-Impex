using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace NaturalStoneImpex.Client.Auth;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private string? _token;

    private static readonly AuthenticationState _anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public string? Token => _token;

    public void SetToken(string? token)
    {
        _token = token;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            return Task.FromResult(_anonymous);
        }

        var claims = ParseClaimsFromJwt(_token);
        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        return Task.FromResult(new AuthenticationState(user));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return Enumerable.Empty<Claim>();
        }

        var payload = parts[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes);

        if (keyValuePairs is null)
        {
            return Enumerable.Empty<Claim>();
        }

        var claims = new List<Claim>();

        foreach (var kvp in keyValuePairs)
        {
            if (kvp.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in kvp.Value.EnumerateArray())
                {
                    claims.Add(new Claim(kvp.Key, element.GetRawText().Trim('"')));
                }
            }
            else
            {
                claims.Add(new Claim(kvp.Key, kvp.Value.GetRawText().Trim('"')));
            }
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64.Replace('-', '+').Replace('_', '/'));
    }
}
