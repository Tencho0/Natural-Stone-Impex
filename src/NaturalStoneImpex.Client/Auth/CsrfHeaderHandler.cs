namespace NaturalStoneImpex.Client.Auth;

/// <summary>
/// Adds the custom header the server requires on cookie-authenticated state-changing API calls.
/// Cross-site forms cannot set it, so it guards against CSRF on top of the SameSite=Strict cookie.
/// </summary>
public class CsrfHeaderHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("X-NSI-Request", "1");
        return base.SendAsync(request, cancellationToken);
    }
}
