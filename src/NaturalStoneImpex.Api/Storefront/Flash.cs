namespace NaturalStoneImpex.Api.Storefront;

/// <summary>One-shot message passed across a redirect in a short-lived cookie.</summary>
public static class Flash
{
    public const string CookieName = "nsi_flash";
    public const string Added = "added";
    public const string Removed = "removed";
    public const string Full = "full";

    public static void Set(HttpResponse response, string code)
        => response.Cookies.Append(CookieName, code, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromSeconds(60),
            IsEssential = true,
            Path = "/"
        });

    /// <summary>Returns the pending known code (and deletes the cookie), or null.</summary>
    public static string? Take(HttpContext context)
    {
        var value = context.Request.Cookies[CookieName];
        if (value is null)
            return null;

        context.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
        return value is Added or Removed or Full ? value : null;
    }
}
