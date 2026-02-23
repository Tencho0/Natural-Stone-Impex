using System.Net.Http.Headers;

namespace NaturalStoneImpex.Client.Auth;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly CustomAuthStateProvider _authStateProvider;

    public AuthTokenHandler(CustomAuthStateProvider authStateProvider)
    {
        _authStateProvider = authStateProvider;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = _authStateProvider.Token;

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
