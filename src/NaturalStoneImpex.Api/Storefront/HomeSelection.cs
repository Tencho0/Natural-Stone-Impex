using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>What the home page shows from the featured selection (spec §7.1).</summary>
/// <param name="Board">The 2×2 sample board in the hero: exactly four products, or none.</param>
/// <param name="Row">"Избрани продукти": up to eight cards, never repeating a board product.</param>
/// <param name="BandPhoto">A product whose photo illustrates the visualizer band, not shown elsewhere on the page.</param>
public sealed record HomeContent(
    IReadOnlyList<ProductListDto> Board,
    IReadOnlyList<ProductListDto> Row,
    ProductListDto? BandPhoto);

public static class HomeSelection
{
    public const int BoardSize = 4;
    public const int RowSize = 8;

    /// <summary>How many featured products the home page asks for: board, row and one band photo.</summary>
    public const int FeaturedFetchCount = BoardSize + RowSize + 1;

    /// <summary>
    /// Splits the featured selection (already in display order) into the board, the card row and the band photo.
    /// The board takes the first four. The row continues the same selection, so a product appears once.
    /// With fewer than four products there is no board, and the row shows them all.
    /// </summary>
    public static HomeContent Split(IReadOnlyList<ProductListDto> featured)
    {
        if (featured.Count < BoardSize)
            return new HomeContent([], featured.ToList(), null);

        var board = featured.Take(BoardSize).ToList();
        var row = featured.Skip(BoardSize).Take(RowSize).ToList();
        var band = featured.Count > BoardSize + RowSize ? featured[BoardSize + RowSize] : null;
        return new HomeContent(board, row, band);
    }

    /// <summary>Categories that have at least one active product, in the given order.</summary>
    public static IReadOnlyList<CategoryDto> VisibleCategories(IEnumerable<CategoryDto> categories)
        => categories.Where(c => c.ProductCount > 0).ToList();
}
