using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class CatalogPagingTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(0, 1)]
    [InlineData(-7, 1)]
    [InlineData(4, 4)]
    public void Normalize_treats_values_below_one_as_the_first_page(int requested, int expected)
        => Assert.Equal(expected, CatalogPaging.Normalize(requested));

    [Theory]
    [InlineData(1, 5, 1)]
    [InlineData(3, 5, 3)]
    [InlineData(5, 5, 5)]
    [InlineData(9, 5, 5)]
    [InlineData(int.MaxValue, 5, 5)]
    [InlineData(0, 5, 1)]
    [InlineData(2, 0, 1)]
    public void Clamp_keeps_the_page_within_the_results(int requested, int totalPages, int expected)
        => Assert.Equal(expected, CatalogPaging.Clamp(requested, totalPages));

    [Theory]
    [InlineData(1, 0, "")]
    [InlineData(1, 1, "1")]
    [InlineData(3, 5, "1 2 3 4 5")]
    [InlineData(4, 7, "1 2 3 4 5 6 7")]
    [InlineData(1, 10, "1 2 … 10")]
    [InlineData(3, 10, "1 2 3 4 … 10")]
    [InlineData(4, 10, "1 2 3 4 5 … 10")]
    [InlineData(5, 10, "1 … 4 5 6 … 10")]
    [InlineData(7, 10, "1 … 6 7 8 9 10")]
    [InlineData(10, 10, "1 … 9 10")]
    public void Window_shows_the_ends_and_the_neighbours_of_the_current_page(int current, int total, string expected)
    {
        var window = CatalogPaging.Window(current, total);
        Assert.Equal(expected, string.Join(' ', window.Select(p => p?.ToString() ?? "…")));
    }
}
