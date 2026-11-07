using Microsoft.AspNetCore.Diagnostics;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>Decides which header link is active for the current path.</summary>
public static class NavMatch
{
    /// <summary>
    /// True when <paramref name="currentPath"/> is <paramref name="linkPath"/> or lies below it.
    /// Product pages belong to the catalog. Case and a trailing slash are ignored.
    /// </summary>
    public static bool IsActive(string? currentPath, string linkPath)
    {
        var current = Normalize(currentPath);
        var link = Normalize(linkPath);

        if (link == "/")
            return current == "/";
        if (Within(current, link))
            return true;
        return link == "/catalog" && Within(current, "/products");
    }

    /// <summary>
    /// Path and query the browser shows for this request. Re-executed error and not-found pages render under
    /// /error or /not-found, but the address bar still holds the original URL. With &lt;base href="/"&gt;, an
    /// in-page link such as the skip link must be built from this, or "#main" would lead to the home page.
    /// </summary>
    public static string BrowserPathAndQuery(HttpContext context)
    {
        string url;
        var reExecute = context.Features.Get<IStatusCodeReExecuteFeature>();
        var exception = context.Features.Get<IExceptionHandlerPathFeature>();
        if (reExecute is not null)
            url = reExecute.OriginalPathBase + reExecute.OriginalPath + reExecute.OriginalQueryString;
        else if (exception is not null)
            url = context.Request.PathBase + exception.Path + context.Request.QueryString; // query is left as sent
        else
            url = context.Request.PathBase + context.Request.Path + context.Request.QueryString;

        // "//host/x" or "/\host" in an href would leave the site; keep it a local path.
        if (url.Length == 0 || url[0] != '/')
            return "/" + url.TrimStart('/', '\\');
        if (url.Length > 1 && (url[1] == '/' || url[1] == '\\'))
            return "/" + url.TrimStart('/', '\\');
        return url;
    }

    /// <summary>Bulgarian accessible text for the cart count, e.g. "1 продукт", "3 продукта".</summary>
    public static string CartCountText(int count)
        => count == 1 ? "1 продукт" : $"{count} продукта";

    private static bool Within(string current, string section)
        => current == section || current.StartsWith(section + "/", StringComparison.Ordinal);

    private static string Normalize(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return "/";
        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
            path = path[..cut];
        path = path.ToLowerInvariant().TrimEnd('/');
        return path.Length == 0 ? "/" : path.StartsWith('/') ? path : "/" + path;
    }
}
