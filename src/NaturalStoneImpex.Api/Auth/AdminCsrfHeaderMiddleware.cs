namespace NaturalStoneImpex.Api.Auth;

/// <summary>
/// CSRF defence in depth: state-changing API calls authenticated by the admin cookie, and
/// cart item posts, must carry the custom header that the WASM client always sends.
/// Cross-site forms cannot set custom headers.
/// </summary>
public class AdminCsrfHeaderMiddleware
{
    public const string HeaderName = "X-NSI-Request";
    public const string HeaderValue = "1";

    private readonly RequestDelegate _next;

    public AdminCsrfHeaderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var authenticatedScheme = context.User.Identity?.IsAuthenticated == true
            ? AuthSchemes.Select(context)
            : null;

        if (!IsAllowed(context.Request.Method, context.Request.Path, authenticatedScheme, context.Request.Headers))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Невалидна заявка." });
            return;
        }

        await _next(context);
    }

    public static bool IsAllowed(string method, PathString path, string? authenticatedScheme, IHeaderDictionary headers)
    {
        var isStateChanging = HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

        if (!isStateChanging || !path.StartsWithSegments("/api"))
            return true;

        var requiresHeader = authenticatedScheme == AuthSchemes.Cookie
            || path.StartsWithSegments("/api/cart/items");

        if (!requiresHeader)
            return true;

        return headers.TryGetValue(HeaderName, out var value) && value == HeaderValue;
    }
}
