using System.Globalization;
using System.Text;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>Builds catalog URLs from the view state (spec §7.2). Defaults are left out of the URL.</summary>
public static class CatalogUrl
{
    public const string Path = "/catalog";

    /// <summary>
    /// "/catalog" plus only the parameters that differ from the default: no categoryId when null, no search when
    /// blank (it is trimmed and escaped), no page when it is 1 or less. Used for links, pagination and the canonical.
    /// </summary>
    public static string Build(int? categoryId, string? search, int page)
    {
        var query = new StringBuilder();

        if (categoryId is { } id)
            Append(query, "categoryId", id.ToString(CultureInfo.InvariantCulture));

        var text = search?.Trim();
        if (!string.IsNullOrEmpty(text))
            Append(query, "search", Uri.EscapeDataString(text));

        if (page > 1)
            Append(query, "page", page.ToString(CultureInfo.InvariantCulture));

        return query.Length == 0 ? Path : $"{Path}?{query}";
    }

    /// <summary>
    /// The categoryId query value, or null when it is missing or not a positive whole number. Query values are
    /// bound as strings because an int that fails to parse ("?categoryId=abc") would otherwise be a 500.
    /// </summary>
    public static int? ParseCategoryId(string? value)
        => int.TryParse(value, NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out var id)
           && id > 0 ? id : null;

    /// <summary>The page query value; anything that is not a whole number of 1 or more means the first page.</summary>
    public static int ParsePage(string? value)
        => int.TryParse(value, NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out var page)
           ? CatalogPaging.Normalize(page) : 1;

    private static void Append(StringBuilder query, string name, string value)
    {
        if (query.Length > 0)
            query.Append('&');
        query.Append(name).Append('=').Append(value);
    }
}
