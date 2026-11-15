using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class CatalogUrlTests
{
    [Theory]
    [InlineData(null, null, 1, "/catalog")]
    [InlineData(3, null, 1, "/catalog?categoryId=3")]
    [InlineData(3, "мрамор плочи", 2, "/catalog?categoryId=3&search=%D0%BC%D1%80%D0%B0%D0%BC%D0%BE%D1%80%20%D0%BF%D0%BB%D0%BE%D1%87%D0%B8&page=2")]
    [InlineData(null, "  ", 1, "/catalog")]
    public void Build_omits_defaults(int? cat, string? q, int page, string expected)
        => Assert.Equal(expected, CatalogUrl.Build(cat, q, page));

    [Theory]
    [InlineData(null, null, 0, "/catalog")]
    [InlineData(null, null, -4, "/catalog")]
    [InlineData(null, null, 3, "/catalog?page=3")]
    [InlineData(null, "гранит", 1, "/catalog?search=%D0%B3%D1%80%D0%B0%D0%BD%D0%B8%D1%82")]
    public void Build_treats_pages_below_one_as_the_first(int? cat, string? q, int page, string expected)
        => Assert.Equal(expected, CatalogUrl.Build(cat, q, page));

    [Fact]
    public void Build_trims_the_search_text()
        => Assert.Equal("/catalog?search=a%20b", CatalogUrl.Build(null, "  a b \t", 1));

    [Fact]
    public void Build_escapes_characters_that_would_break_the_query()
        => Assert.Equal("/catalog?search=a%26page%3D9%23x%2By", CatalogUrl.Build(null, "a&page=9#x+y", 1));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("3", 3)]
    [InlineData(" 12 ", 12)]
    [InlineData("abc", null)]
    [InlineData("0", null)]
    [InlineData("-2", null)]
    [InlineData("3.5", null)]
    [InlineData("99999999999", null)]
    public void ParseCategoryId_accepts_only_positive_whole_numbers(string? value, int? expected)
        => Assert.Equal(expected, CatalogUrl.ParseCategoryId(value));

    [Theory]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("2", 2)]
    [InlineData("0", 1)]
    [InlineData("-5", 1)]
    [InlineData("abc", 1)]
    [InlineData("2abc", 1)]
    [InlineData("99999999999", 1)]
    public void ParsePage_falls_back_to_the_first_page(string? value, int expected)
        => Assert.Equal(expected, CatalogUrl.ParsePage(value));
}
