using System.Text.Encodings.Web;
using System.Text.Json;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>Pure helpers behind SeoHead: page titles, absolute URLs and safe JSON-LD.</summary>
public static class SeoFormat
{
    public const string SiteName = "Natural Stone Impex";

    private static readonly JsonSerializerOptions JsonLdOptions = new()
    {
        // The default encoder escapes <, >, & and ' so the JSON cannot close the script element.
        Encoder = JavaScriptEncoder.Default
    };

    /// <summary>"{title} | Natural Stone Impex", or the site name alone when the title is empty.</summary>
    public static string Title(string? title)
        => string.IsNullOrWhiteSpace(title) ? SiteName : $"{title.Trim()} | {SiteName}";

    /// <summary>
    /// Absolute URL on the current origin. <paramref name="path"/> must be a local path; anything that is not
    /// (a scheme, a protocol-relative "//host" or a backslash trick) falls back to the site root.
    /// </summary>
    public static string AbsoluteUrl(string scheme, string host, string? pathBase, string? path)
    {
        var origin = $"{scheme}://{host}{(pathBase ?? string.Empty).TrimEnd('/')}";
        return origin + LocalPath(path);
    }

    /// <summary>Image URL for og:image: absolute http(s) URLs pass through, local paths are made absolute.</summary>
    public static string ImageUrl(string scheme, string host, string? pathBase, string imagePath)
        => (imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            && Uri.TryCreate(imagePath, UriKind.Absolute, out var uri)
                ? uri.ToString()
                : AbsoluteUrl(scheme, host, pathBase, imagePath);

    /// <summary>JSON-LD body that is safe inside a script element. Strings are taken as JSON already.</summary>
    public static string JsonLd(object value)
        => value is string json
            ? json.Replace("<", "\\u003C", StringComparison.Ordinal)
            : JsonSerializer.Serialize(value, value.GetType(), JsonLdOptions);

    private static string LocalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";
        path = path.Trim();
        if (!path.StartsWith('/'))
        {
            if (path.Contains(':'))
                return "/"; // "https://other.host", "javascript:..."
            path = "/" + path;
        }
        if (path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\'))
            return "/";
        return path;
    }
}
