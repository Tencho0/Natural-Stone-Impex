namespace NaturalStoneImpex.Api.Storefront;

/// <summary>Page arithmetic for the catalog: which page to show and which page links to offer.</summary>
public static class CatalogPaging
{
    public const int PageSize = 24;

    /// <summary>A page number from the query string: 0 or below means the first page.</summary>
    public static int Normalize(int page) => Math.Max(1, page);

    /// <summary>
    /// The page to render: past the last page shows the last page, and with no results the first.
    /// The canonical URL then names the page actually shown.
    /// </summary>
    public static int Clamp(int page, int totalPages) => Math.Clamp(page, 1, Math.Max(1, totalPages));

    /// <summary>
    /// Page links to render: the first and last page, the current page and its neighbours. Null marks a gap
    /// (rendered as "…"). A gap that would hide a single page shows that page instead.
    /// </summary>
    public static IReadOnlyList<int?> Window(int current, int totalPages)
    {
        if (totalPages <= 0)
            return [];

        current = Clamp(current, totalPages);
        var pages = new SortedSet<int> { 1, totalPages };
        for (var p = current - 1; p <= current + 1; p++)
        {
            if (p >= 1 && p <= totalPages)
                pages.Add(p);
        }

        var window = new List<int?>();
        var previous = 0;
        foreach (var page in pages)
        {
            if (page - previous == 2)
                window.Add(page - 1);
            else if (page - previous > 2)
                window.Add(null);
            window.Add(page);
            previous = page;
        }
        return window;
    }
}
