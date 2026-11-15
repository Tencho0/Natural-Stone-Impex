using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Storefront;

namespace NaturalStoneImpex.Api.Tests.Storefront;

public class HomeSelectionTests
{
    private static List<ProductListDto> Featured(int count)
        => Enumerable.Range(1, count)
            .Select(i => new ProductListDto { Id = i, Name = $"P{i}", ImagePath = $"/uploads/products/product-{i}.jpg" })
            .ToList();

    private static int[] Ids(IEnumerable<ProductListDto> products) => products.Select(p => p.Id).ToArray();

    [Fact]
    public void Fetch_count_covers_board_row_and_band_photo()
        => Assert.Equal(13, HomeSelection.FeaturedFetchCount);

    [Fact]
    public void Full_selection_puts_four_on_the_board_and_the_next_eight_in_the_row()
    {
        var home = HomeSelection.Split(Featured(13));

        Assert.Equal([1, 2, 3, 4], Ids(home.Board));
        Assert.Equal([5, 6, 7, 8, 9, 10, 11, 12], Ids(home.Row));
        Assert.Equal(13, home.BandPhoto?.Id);
    }

    [Fact]
    public void Row_never_repeats_a_board_product()
    {
        var home = HomeSelection.Split(Featured(13));

        Assert.Empty(Ids(home.Board).Intersect(Ids(home.Row)));
    }

    [Fact]
    public void Band_photo_is_absent_when_no_product_is_left_over()
    {
        var home = HomeSelection.Split(Featured(12));

        Assert.Equal(8, home.Row.Count);
        Assert.Null(home.BandPhoto);
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(9, 5)]
    public void Short_selection_fills_the_board_first(int available, int expectedRow)
    {
        var home = HomeSelection.Split(Featured(available));

        Assert.Equal(4, home.Board.Count);
        Assert.Equal(expectedRow, home.Row.Count);
        Assert.Null(home.BandPhoto);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Fewer_than_four_drops_the_board_and_shows_them_as_cards(int available)
    {
        var home = HomeSelection.Split(Featured(available));

        Assert.Empty(home.Board);
        Assert.Equal(Ids(Featured(available)), Ids(home.Row));
        Assert.Null(home.BandPhoto);
    }

    [Fact]
    public void More_than_fetched_is_capped()
    {
        var home = HomeSelection.Split(Featured(40));

        Assert.Equal(4, home.Board.Count);
        Assert.Equal(8, home.Row.Count);
        Assert.Equal(13, home.BandPhoto?.Id);
    }

    [Fact]
    public void Categories_without_products_are_hidden_and_order_is_kept()
    {
        var categories = new List<CategoryDto>
        {
            new() { Id = 5, Name = "Варовик", ProductCount = 21 },
            new() { Id = 7, Name = "Празна", ProductCount = 0 },
            new() { Id = 2, Name = "Гнайс", ProductCount = 66 }
        };

        var visible = HomeSelection.VisibleCategories(categories);

        Assert.Equal([5, 2], visible.Select(c => c.Id).ToArray());
    }
}
