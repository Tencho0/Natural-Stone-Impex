namespace NaturalStoneImpex.Api.Auth;

/// <summary>
/// Accepts a login return URL only when it is a local path on this site; anything else
/// (protocol-relative, absolute, scheme or encoded tricks) falls back to the default.
/// </summary>
public static class ReturnUrlValidator
{
    public static string ToLocalOrDefault(string? returnUrl, string fallback = "/admin")
    {
        if (string.IsNullOrEmpty(returnUrl))
            return fallback;

        var decoded = Uri.UnescapeDataString(returnUrl);

        return IsLocalPath(returnUrl) && IsLocalPath(decoded) ? returnUrl : fallback;
    }

    private static bool IsLocalPath(string url)
    {
        if (url.Length == 0 || url[0] != '/')
            return false;

        if (url.Length > 1 && (url[1] == '/' || url[1] == '\\'))
            return false;

        // Browsers strip tabs and newlines, so "/\t/evil.com" would become "//evil.com".
        if (url.Any(char.IsControl))
            return false;

        return Uri.TryCreate(url, UriKind.Relative, out _);
    }
}
