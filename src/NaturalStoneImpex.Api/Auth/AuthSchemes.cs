using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace NaturalStoneImpex.Api.Auth;

/// <summary>
/// Authentication scheme names. The policy scheme forwards to JWT Bearer when the request
/// carries a Bearer token (API clients, Swagger) and to the admin cookie otherwise.
/// </summary>
public static class AuthSchemes
{
    public const string Policy = "NsiAuth";
    public const string Cookie = "NsiAdminCookie";
    public const string Bearer = JwtBearerDefaults.AuthenticationScheme;

    public static string Select(HttpContext ctx)
    {
        string? authorization = ctx.Request.Headers.Authorization;
        return authorization is not null
            && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? Bearer
            : Cookie;
    }
}
